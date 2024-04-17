// <copyright file="ExistingBuildingSystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using Colossal.Entities;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Notifications;
    using Game.Prefabs;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System to identify any existing and unclassified buildings (not tagged as either spawned or plopped) on save load.
    /// The default is to classify them as spawned (for safety).
    /// </summary>
    public partial class ExistingBuildingSystem : GameSystemBase
    {
        // Queries.
        private EntityQuery _emptyQuery;
        private EntityQuery _allLockedBuildingsQuery;
        private EntityQuery _allUnlockedBuildingsQuery;
        private EntityQuery _abandonedBuildingsQuery;
        private EntityQuery _buildingConfigurationQuery;

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
        /// Removes abandonment from all eligible buildings.
        /// </summary>
        internal void RemoveAllAbandonment()
        {
            // Get references.
            IconCommandSystem iconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
            IconCommandBuffer iconCommandBuffer = iconCommandSystem.CreateCommandBuffer();
            BuildingConfigurationData buildingConfigurationData = _buildingConfigurationQuery.GetSingleton<BuildingConfigurationData>();
            Entity abandonedNotification = buildingConfigurationData.m_AbandonedNotification;

            foreach (Entity entity in _abandonedBuildingsQuery.ToEntityArray(Allocator.Temp))
            {
                EntityManager.RemoveComponent<Abandoned>(entity);

                // Take property off market.
                EntityManager.AddComponent<PropertyToBeOnMarket>(entity);
                if (EntityManager.HasComponent<PropertyOnMarket>(entity))
                {
                    EntityManager.RemoveComponent<PropertyOnMarket>(entity);
                }

                // Reset building condition.
                if (EntityManager.HasComponent<BuildingCondition>(entity))
                {
                    EntityManager.SetComponentData(entity, new BuildingCondition { m_Condition = 0 });
                }

                // Reset garbage production (removed when abandoned).
                EntityManager.AddComponentData(entity, default(GarbageProducer));

                // Reset mail production (removed when abandoned).
                EntityManager.AddComponentData(entity, default(MailProducer));

                // Reset electricity consumption (removed when abandoned).
                EntityManager.AddComponentData(entity, default(ElectricityConsumer));

                // Reset water consumption (removed when abandoned).
                EntityManager.AddComponentData(entity, default(WaterConsumer));

                // Remove abandoned notification.
                iconCommandBuffer.Remove(entity, abandonedNotification);

                // Update road to refresh utility connections.
                if (EntityManager.TryGetComponent(entity, out Building building) && building.m_RoadEdge != Entity.Null)
                {
                    EntityManager.AddComponent<Updated>(building.m_RoadEdge);
                }
            }
        }

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
            _abandonedBuildingsQuery = SystemAPI.QueryBuilder().WithAll<Building, Abandoned>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().Build();
            _emptyQuery = SystemAPI.QueryBuilder().WithAll<Building>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().WithNone<Signature, PloppedBuilding, SpawnedBuilding>().Build();
            _buildingConfigurationQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
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
