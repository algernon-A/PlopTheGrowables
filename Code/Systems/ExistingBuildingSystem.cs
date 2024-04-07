// <copyright file="ExistingBuildingSystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using Game;
    using Game.Buildings;
    using Unity.Entities;

    /// <summary>
    /// System to identify any existing and unclassified buildings (not tagged as either spawned or plopped) on save load.
    /// The default is to classify them as spawned (for safety).
    /// </summary>
    public partial class ExistingBuildingSystem : GameSystemBase
    {
        private EntityQuery _emptyQuery;

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            // Initialise query.
            _emptyQuery = SystemAPI.QueryBuilder().WithAll<Building>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().WithNone<Signature, PloppedBuilding, SpawnedBuilding>().Build();
            RequireForUpdate(_emptyQuery);
        }

        /// <summary>
        /// Called every update.
        /// </summary>
        protected override void OnUpdate()
        {
            // Set any existing uncategorised buildings as spawned.
            Mod.Instance.Log.Info($"Setting {_emptyQuery.CalculateEntityCount()} existing buildings as spawned.");
            EntityManager.AddComponent<SpawnedBuilding>(_emptyQuery);
        }
    }
}
