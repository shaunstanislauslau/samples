﻿// <SnippetAddUsings>
using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
// </SnippetAddUsings>

namespace TextClassificationTF
{
    class Program
    {
        // <SnippetDeclareGlobalVariables>
        public const int FeatureLength = 600;
        static readonly string _modelPath = Path.Combine(Environment.CurrentDirectory, "sentiment_model");
        // </SnippetDeclareGlobalVariables>

        static void Main(string[] args)
        {
            // Create MLContext to be shared across the model creation workflow objects 
            // <SnippetCreateMLContext>
            MLContext mlContext = new MLContext();
            // </SnippetCreateMLContext>

            // Dictionary to encode words as integers.
            // <SnippetCreateLookupMap>
            var lookupMap = mlContext.Data.LoadFromTextFile(Path.Combine(_modelPath, "imdb_word_index.csv"),
                columns: new[]
                   {
                        new TextLoader.Column("Words", DataKind.String, 0),
                        new TextLoader.Column("Ids", DataKind.Int32, 1),
                   },
                separatorChar: ','
               );
            // </SnippetCreateLookupMap>

            // The model expects the input feature vector to be a fixed length vector.
            // This action resizes the integer vector to a fixed length vector. If there
            // are less than 600 words in the sentence, the remaining indices will be filled
            // with zeros. If there are more than 600 words in the sentence, then the
            // array is truncated at 600.
            // <SnippetResizeFeatures>
            Action<MovieReview, FixedLengthFeatures> ResizeFeaturesAction = (s, f) =>
            {
                var features = s.VariableLengthFeatures;
                Array.Resize(ref features, FeatureLength);
                f.Features = features;
            };
            // </SnippetResizeFeatures>

            // Load the TensorFlow model.
            // <SnippetLoadTensorFlowModel>
            TensorFlowModel tensorFlowModel = mlContext.Model.LoadTensorFlowModel(_modelPath);
            // </SnippetLoadTensorFlowModel>

            // <SnippetGetModelSchema>
            DataViewSchema schema = tensorFlowModel.GetModelSchema();
            Console.WriteLine(" =============== TensorFlow Model Schema =============== ");
            var featuresType = (VectorDataViewType)schema["Features"].Type;
            Console.WriteLine($"Name: Features, Type: {featuresType.ItemType.RawType}, Size: ({featuresType.Dimensions[0]})");
            var predictionType = (VectorDataViewType)schema["Prediction/Softmax"].Type;
            Console.WriteLine($"Name: Prediction/Softmax, Type: {predictionType.ItemType.RawType}, Size: ({predictionType.Dimensions[0]})");

            // </SnippetGetModelSchema>

            // <SnippetTokenizeIntoWords>
            IEstimator<ITransformer> pipeline =
                // Split the text into individual words
                mlContext.Transforms.Text.TokenizeIntoWords("TokenizedWords", "ReviewText")
                // </SnippetTokenizeIntoWords>

                // <SnippetMapValue>
                // Map each word to an integer value. The array of integer makes up the input features.
                .Append(mlContext.Transforms.Conversion.MapValue("VariableLengthFeatures", lookupMap,
                    lookupMap.Schema["Words"], lookupMap.Schema["Ids"], "TokenizedWords"))
                // </SnippetMapValue>

                // <SnippetCustomMapping>
                // Resize variable length vector to fixed length vector.
                .Append(mlContext.Transforms.CustomMapping(ResizeFeaturesAction, "Resize"))
                // </SnippetCustomMapping>

                // <SnippetScoreTensorFlowModel>
                // Passes the data to TensorFlow for scoring
                .Append(tensorFlowModel.ScoreTensorFlowModel("Prediction/Softmax", "Features"))
                // </SnippetScoreTensorFlowModel>

                // <SnippetCopyColumns>
                // Retrieves the 'Prediction' from TensorFlow and and copies to a column
                .Append(mlContext.Transforms.CopyColumns("Prediction", "Prediction/Softmax"));
            // </SnippetCopyColumns>

            // <SnippetCreateModel>
            // Create an executable model from the estimator pipeline
            IDataView dataView = mlContext.Data.LoadFromEnumerable(new List<MovieReview>());
            ITransformer model = pipeline.Fit(dataView);
            // </SnippetCreateModel>

            // <SnippetCallPredictSentiment>
            PredictSentiment(mlContext, model);
            // </SnippetCallPredictSentiment>
        }

        public static void PredictSentiment(MLContext mlContext, ITransformer model)
        {
            // <SnippetCreatePredictionEngine>
            var engine = mlContext.Model.CreatePredictionEngine<MovieReview, MovieReviewSentimentPrediction>(model);
            // </SnippetCreatePredictionEngine>

            // <SnippetCreateTestData>
            var review = new MovieReview()
            {
                ReviewText = "this film is really good"
            };
            // </SnippetCreateTestData>

            // Predict with TensorFlow pipeline.
            // <SnippetPredict>  
            var sentimentPrediction = engine.Predict(review);
            // </SnippetPredict>  

            // <SnippetDisplayPredictions>
            Console.WriteLine("Number of classes: {0}", sentimentPrediction.Prediction.Length);
            Console.WriteLine("Is sentiment/review positive? {0}", sentimentPrediction.Prediction[1] > 0.5 ? "Yes." : "No.");
            // </SnippetDisplayPredictions>

            /////////////////////////////////// Expected output ///////////////////////////////////
            // 
            // Name: Features, Type: System.Int32, Size: 600
            // Name: Prediction/Softmax, Type: System.Single, Size: 2
            // 
            // Number of classes: 2
            // Is sentiment/review positive ? Yes
            // Prediction Confidence: 0.65
        }

        // <SnippetMovieReviewClass>
        /// <summary>
        /// Class to hold original sentiment data.
        /// </summary>
        public class MovieReview
        {
            public string ReviewText { get; set; }

            /// <summary>
            /// This is a variable length vector designated by VectorType attribute.
            /// Variable length vectors are produced by applying operations such as 'TokenizeWords' on strings
            /// resulting in vectors of tokens of variable lengths.
            /// </summary>
            [VectorType]
            public int[] VariableLengthFeatures { get; set; }
        }
        //</SnippetMovieReviewClass>

        //<SnippetPrediction>
        /// <summary>
        /// Class to contain the output values from the transformation.
        /// </summary>
        public class MovieReviewSentimentPrediction
        {
            [VectorType(2)]
            public float[] Prediction { get; set; }
        }
        // </SnippetPrediction>

        // <SnippetFixedLengthFeatures>
        /// <summary>
        /// Class to hold the fixed length feature vector. Used by the custom mapping
        /// action
        /// </summary>
        public class FixedLengthFeatures
        {
            [VectorType(FeatureLength)]
            public int[] Features { get; set; }
        }
        // </SnippeteFixedLengthFeatures>
    }
}
