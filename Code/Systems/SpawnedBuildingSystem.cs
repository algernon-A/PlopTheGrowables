// <copyright file="SpawnedBuildingSystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using Game;
    using Game.Buildings;
    using Game.Objects;
    using Unity.Entities;

    /// <summary>
    /// System to identify newly-created spawned buildings.
    /// </summary>
    public partial class SpawnedBuildingSystem : GameSystemBase
    {
        private EntityQuery _constructionQuery;

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            // Initialise query.
            _constructionQuery = SystemAPI.QueryBuilder().WithAll<Building, UnderConstruction>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().WithNone<Signature, SpawnedBuilding, PloppedBuilding>().Build();
            RequireForUpdate(_constructionQuery);
        }

        /// <summary>
        /// Called every update.
        /// </summary>
        protected override void OnUpdate()
        {
            // Add spawned tag to under-construction buildings.
            EntityManager.AddComponent<SpawnedBuilding>(_constructionQuery);
        }
    }
}
