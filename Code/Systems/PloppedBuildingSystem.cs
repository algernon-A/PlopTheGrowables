// <copyright file="PloppedBuildingSystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using Colossal.Logging;
    using Game;
    using Game.Buildings;
    using Game.Objects;
    using Unity.Entities;

    /// <summary>
    /// System to identify newly-created plopped buildings..
    /// </summary>
    public partial class PloppedBuildingSystem : GameSystemBase
    {
        // Component typesets (for adding components).
        private readonly ComponentTypeSet _ploppedOnly = new (typeof(PloppedBuilding));
        private readonly ComponentTypeSet _lockedAndPlopped = new (typeof(LevelLocked), typeof(PloppedBuilding));

        // References.
        private EntityQuery _emptyQuery;
        private ILog _log;

        /// <summary>
        /// Gets the active instance record.
        /// </summary>
        public static PloppedBuildingSystem Instance { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether plopped buildings should be automatically level-locked on placement.
        /// </summary>
        public bool LockPloppedBuildings { get; set; } = false;

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            // Set references.
            Instance = this;
            _log = Mod.Instance.Log;

            // Initialise query.
            _emptyQuery = SystemAPI.QueryBuilder().WithAll<Building>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().WithNone<Signature, UnderConstruction, SpawnedBuilding, PloppedBuilding>().Build();
            RequireForUpdate(_emptyQuery);

            // Set state from current settings.
            if (Mod.Instance.ActiveSettings is ModSettings activeSettings)
            {
                LockPloppedBuildings = activeSettings.LockPloppedBuildings;
            }
        }

        /// <summary>
        /// Called every update.
        /// </summary>
        protected override void OnUpdate()
        {
            // Tag any newly-plopped buildings as plopped, and level-lock them if that setting is set.
            EntityManager.AddComponent(_emptyQuery, LockPloppedBuildings ? _lockedAndPlopped : _ploppedOnly);
        }
    }
}
