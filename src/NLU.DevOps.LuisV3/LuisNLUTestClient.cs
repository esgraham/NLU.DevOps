﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace NLU.DevOps.Luis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Logging;
    using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
    using Microsoft.Extensions.Logging;
    using Models;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Test a LUIS model with text and speech.
    /// Implementation of <see cref="INLUTestClient"/>
    /// </summary>
    public sealed class LuisNLUTestClient : INLUTestClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LuisNLUTestClient"/> class.
        /// </summary>
        /// <param name="luisSettings">LUIS settings.</param>
        /// <param name="luisClient">LUIS client.</param>
        public LuisNLUTestClient(LuisSettings luisSettings, ILuisTestClient luisClient)
        {
            this.LuisSettings = luisSettings ?? throw new ArgumentNullException(nameof(luisSettings));
            this.LuisClient = luisClient ?? throw new ArgumentNullException(nameof(luisClient));
        }

        private static ILogger Logger => LazyLogger.Value;

        private static Lazy<ILogger> LazyLogger { get; } = new Lazy<ILogger>(() => ApplicationLogger.LoggerFactory.CreateLogger<LuisNLUTestClient>());

        private LuisSettings LuisSettings { get; }

        private ILuisTestClient LuisClient { get; }

        /// <inheritdoc />
        public async Task<LabeledUtterance> TestAsync(
            JToken query,
            CancellationToken cancellationToken)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var predictionRequest = query.ToObject<PredictionRequest>();
            predictionRequest.Query = predictionRequest.Query ?? query.Value<string>("text");
            var luisResult = await this.LuisClient.QueryAsync(predictionRequest, cancellationToken).ConfigureAwait(false);
            return this.LuisResultToLabeledUtterance(luisResult);
        }

        /// <inheritdoc />
        public async Task<LabeledUtterance> TestSpeechAsync(
            string speechFile,
            JToken query,
            CancellationToken cancellationToken)
        {
            if (speechFile == null)
            {
                throw new ArgumentNullException(nameof(speechFile));
            }

            var predictionRequest = query?.ToObject<PredictionRequest>();
            if (predictionRequest != null)
            {
                predictionRequest.Query = predictionRequest.Query ?? query.Value<string>("text");
            }

            var luisResult = await this.LuisClient.RecognizeSpeechAsync(speechFile, predictionRequest, cancellationToken).ConfigureAwait(false);
            return this.LuisResultToLabeledUtterance(luisResult);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.LuisClient.Dispose();
        }

        private static IEnumerable<Entity> GetEntities(string utterance, IDictionary<string, object> entities, IDictionary<string, string> mappedTypes)
        {
            if (entities == null)
            {
                return null;
            }

            Entity getEntity(string entityType, JToken entityJson, JToken entityMetadata)
            {
                var startIndex = entityMetadata.Value<int>("startIndex");
                var length = entityMetadata.Value<int>("length");
                var score = entityMetadata.Value<double?>("score");
                var matchText = utterance.Substring(startIndex, length);
                var matchIndex = 0;
                var currentStart = 0;
                var nextStart = 0;
                while ((nextStart = utterance.IndexOf(matchText, currentStart, StringComparison.Ordinal)) != startIndex)
                {
                    ++matchIndex;
                    currentStart = nextStart + 1;
                }

                var entityValue = PruneMetadata(entityJson);

                var modifiedEntityType = entityType;
                if (mappedTypes.TryGetValue(entityType, out var mappedEntityType))
                {
                    modifiedEntityType = mappedEntityType;
                }

                var entityResolution = entityMetadata["resolution"];
                return score.HasValue
                    ? new ScoredEntity(modifiedEntityType, entityValue, entityResolution, matchText, matchIndex, score.Value)
                    : new Entity(modifiedEntityType, entityValue, entityResolution, matchText, matchIndex);
            }

            var instanceMetadata = default(JObject);
            if (entities.TryGetValue("$instance", out var instanceJson))
            {
                instanceMetadata = instanceJson as JObject;
            }

            return entities
                .Where(pair => pair.Key != "$instance")
                .Select(pair =>
                    new
                    {
                        EntityType = pair.Key,
                        Entities = ((JArray)pair.Value).Zip(
                            instanceMetadata?[pair.Key],
                            (entityValue, entityMetadata) =>
                                new
                                {
                                    EntityValue = entityValue,
                                    EntityMetadata = entityMetadata
                                })
                    })
                .SelectMany(entityInfo =>
                    entityInfo.Entities.Select(entity =>
                        getEntity(entityInfo.EntityType, entity.EntityValue, entity.EntityMetadata)));
        }

        private static JToken PruneMetadata(JToken json)
        {
            if (json is JObject jsonObject)
            {
                var prunedObject = new JObject();
                foreach (var property in jsonObject.Properties())
                {
                    if (property.Name != "$instance")
                    {
                        prunedObject.Add(property.Name, PruneMetadata(property.Value));
                    }
                }

                return prunedObject;
            }

            return json;
        }

        private LabeledUtterance LuisResultToLabeledUtterance(PredictionResponse predictionResponse)
        {
            if (predictionResponse == null)
            {
                return new LabeledUtterance(null, null, null);
            }

            var mappedTypes = this.LuisSettings.PrebuiltEntityTypes
                .ToDictionary(pair => $"builtin.{pair.Value}", pair => pair.Key);

            var intent = predictionResponse.Prediction.TopIntent;
            var entities = GetEntities(predictionResponse.Query, predictionResponse.Prediction.Entities, mappedTypes)?.ToList();
            var intentData = default(Intent);
            predictionResponse.Prediction.Intents?.TryGetValue(intent, out intentData);
            return intentData != null && intentData.Score.HasValue
                ? new ScoredLabeledUtterance(predictionResponse.Query, intent, intentData.Score.Value, entities)
                : new LabeledUtterance(predictionResponse.Query, intent, entities);
        }
    }
}
