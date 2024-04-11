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
        private EntityQuery _allLockedBuildingsQuery;
        private EntityQuery _allUnlockedBuildingsQuery;

        /// <summary>
        /// Gets the active instance.
        /// </summary>
        public static ExistingBuildingSystem Instance { get; private set; }

        /// <summary>
        /// Applies level-locking to all eligible buildings.
        /// </summary>
        internal void LockAllBuildings() => EntityManager.AddComponent<LevelLocked>(_allUnlockedBuildingsQuery);

        /// <summary>
        /// Removes level-locking from all eligible buildings.
        /// </summary>
        internal void UnlockAllBuildings() => EntityManager.RemoveComponent<LevelLocked>(_allLockedBuildingsQuery);

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            Instance = this;

            base.OnCreate();

            // Initialise queries.
            _allLockedBuildingsQuery = SystemAPI.QueryBuilder().WithAll<Building, LevelLocked>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().WithNone<Signature>().Build();
            _allUnlockedBuildingsQuery = SystemAPI.QueryBuilder().WithAll<Building>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().WithNone<Signature, LevelLocked>().Build();
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

        /// <summary>
        /// Called when the system is destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            Instance = null;
            base.OnDestroy();
        }
    }
}
