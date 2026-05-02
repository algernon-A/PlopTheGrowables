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
    using Game.City;
    using Game.Common;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Simulation;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System to identify any existing and unclassified buildings (not tagged as either spawned or plopped) on save load.
    /// The default is to classify them as spawned (for safety).
    /// </summary>
    public partial class ExistingBuildingSystem : GameSystemBase
    {
        // Queries.
        private EntityQuery _allBuildingsQuery;
        private EntityQuery _abandonedBuildingsQuery;
        private EntityQuery _untaggedQuery;
        private EntityQuery _levelLockedQuery;
        private EntityQuery _buildingConfigurationQuery;

        /// <summary>
        /// Gets the active instance.
        /// </summary>
        public static ExistingBuildingSystem Instance { get; private set; }

        /// <summary>
        /// Applies level-locking to all eligible buildings.
        /// </summary>
        internal void LockAllBuildings()
        {
            foreach (Entity entity in _allBuildingsQuery.ToEntityArray(Allocator.Temp))
            {
                MakeHistorical(entity);
            }
        }

        /// <summary>
        /// Removes level-locking from all eligible buildings.
        /// </summary>
        internal void UnlockAllBuildings()
        {
            foreach (Entity entity in _allBuildingsQuery.ToEntityArray(Allocator.Temp))
            {
                RemoveHistorical(entity);
            }
        }

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
            _allBuildingsQuery = SystemAPI.QueryBuilder().WithAll<Building>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().WithNone<Signature>().Build();
            _abandonedBuildingsQuery = SystemAPI.QueryBuilder().WithAll<Building, Abandoned>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().Build();
            _untaggedQuery = SystemAPI.QueryBuilder().WithAll<Building>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().WithNone<Signature, PloppedBuilding, SpawnedBuilding>().Build();
            _levelLockedQuery = SystemAPI.QueryBuilder().WithAll<Building>().WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>().WithAll<LevelLocked>().Build();
            _buildingConfigurationQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());

            RequireAnyForUpdate(_untaggedQuery, _levelLockedQuery);
        }

        /// <summary>
        /// Called every update.
        /// </summary>
        protected override void OnUpdate()
        {
            // Add 'spawned' tag to any buildings that don't have any existing plopped or spawned tag.
            if (!_untaggedQuery.IsEmpty)
            {
                Mod.Instance.Log.Info($"Found {_untaggedQuery.CalculateEntityCount()} existing buildings without a category.");

                // Set any existing uncategorised buildings as spawned.
                Mod.Instance.Log.Info($"Setting {_untaggedQuery.CalculateEntityCount()} existing buildings as spawned.");
                EntityManager.AddComponent<SpawnedBuilding>(_untaggedQuery);
            }

            // Convert any existing buildings with the level-locked tag to use the game's historical flag instead.
            if (!_levelLockedQuery.IsEmpty)
            {
                NativeArray<Entity> entityArray = _levelLockedQuery.ToEntityArray(Allocator.Temp);
                Mod.Instance.Log.Info($"Found {_levelLockedQuery.CalculateEntityCount()} existing buildings with legacy level locked tag.");

                foreach (Entity entity in entityArray)
                {
                    MakeHistorical(entity);
                    EntityManager.RemoveComponent<LevelLocked>(entity);
                }
            }
        }

        /// <summary>
        /// Called when the system is destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            Instance = null;
            base.OnDestroy();
        }

        /// <summary>
        /// Marks the given building as historical, which has the effect of level-locking it and preventing it from being automatically upgraded by the game.
        /// </summary>
        /// <param name="entity">Building entity to make historical.</param>
        private void MakeHistorical(Entity entity)
        {
            Building building = EntityManager.GetComponentData<Building>(entity);
            building.m_Flags |= Game.Buildings.BuildingFlags.Historical;
            BuildingCondition buildingCondition = EntityManager.GetComponentData<BuildingCondition>(entity);
            buildingCondition.m_Condition = 0;
            EntityManager.SetComponentData(entity, building);
            EntityManager.SetComponentData(entity, buildingCondition);
        }

        /// <summary>
        /// Removes historical status from the given building.
        /// </summary>
        /// <param name="entity">Building entity to remove historical status from.</param>
        private void RemoveHistorical(Entity entity)
        {
            Building building = EntityManager.GetComponentData<Building>(entity);
            building.m_Flags &= ~Game.Buildings.BuildingFlags.Historical;
            EntityManager.SetComponentData(entity, building);
        }
    }
}
