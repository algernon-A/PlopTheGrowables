// <copyright file="HistoricalLevellingSystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using System.Reflection;
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Triggers;
    using Game.Zones;
    using HarmonyLib;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using BuildingFlags = Game.Prefabs.BuildingFlags;

    /// <summary>
    /// Custom building levelling system to implement optional level locking for growables.
    /// </summary>
    public partial class HistoricalLevellingSystem : GameSystemBase
    {
        // Building levelling queues.
        private NativeQueue<Entity> _levelupQueue;
        private NativeQueue<Entity> _leveldownQueue;

        // System references.
        private SimulationSystem _simulationSystem;
        private TriggerSystem _triggerSystem;
        private Game.Zones.SearchSystem _zoneSearchSystem;
        private ZoneBuiltRequirementSystem _zoneBuiltRequirementSystem;
        private IconCommandSystem _iconCommandSystem;
        private ElectricityRoadConnectionGraphSystem _electricityRoadConnectionGraphSystem;
        private WaterPipeRoadConnectionGraphSystem _waterPipeRoadConnectionGraphSystem;

        // Queries.
        private EntityQuery _buildingPrefabGroupQuery;
        private EntityQuery _buildingSettingsQuery;

        // Frame barrier.
        private EndFrameBarrier _endFrameBarrier;

        /// <summary>
        /// Gets the active instance.
        /// </summary>
        public static HistoricalLevellingSystem Instance { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether building level changes should be disabled.
        /// </summary>
        public bool DisableLevelling { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether building abandonment should be prevented.
        /// </summary>
        public bool DisableAbandonment { get; set; } = false;

        /// <summary>
        /// Updates the active level up and level down queues to the provided values.
        /// </summary>
        /// <param name="levelUpQueue">Level up queue to set.</param>
        /// <param name="levelDownQueue">Level down queue to set.</param>
        internal void SetLevelQueues(NativeQueue<Entity> levelUpQueue, NativeQueue<Entity> levelDownQueue)
        {
            Mod.Instance.Log.Info("Updating level queues");
            _levelupQueue = levelUpQueue;
            _leveldownQueue = levelDownQueue;
        }

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            Instance = this;

            base.OnCreate();

            // Set references.
            _simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            _triggerSystem = World.GetOrCreateSystemManaged<TriggerSystem>();
            _zoneSearchSystem = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            _zoneBuiltRequirementSystem = World.GetOrCreateSystemManaged<ZoneBuiltRequirementSystem>();
            _iconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
            _electricityRoadConnectionGraphSystem = World.GetOrCreateSystemManaged<ElectricityRoadConnectionGraphSystem>();
            _waterPipeRoadConnectionGraphSystem = World.GetOrCreateSystemManaged<WaterPipeRoadConnectionGraphSystem>();
            _buildingPrefabGroupQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingData>(), ComponentType.ReadOnly<BuildingSpawnGroupData>(), ComponentType.ReadOnly<PrefabData>());
            _buildingSettingsQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
            _endFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            RequireForUpdate(_buildingSettingsQuery);

            // Reflect level up queue.
            FieldInfo m_LevelupQueueField = AccessTools.Field(typeof(PropertyRenterSystem), "m_LevelupQueue");
            if (m_LevelupQueueField is null)
            {
                Mod.Instance.Log.Error("Unable to get LevelupQueue FieldInfo");
                Enabled = false;
                return;
            }

            _levelupQueue = (NativeQueue<Entity>)m_LevelupQueueField.GetValue(World.GetOrCreateSystemManaged<PropertyRenterSystem>());

            // Reflect level down queue.
            FieldInfo m_LeveldownQueueField = AccessTools.Field(typeof(PropertyRenterSystem), "m_LeveldownQueue");
            if (m_LeveldownQueueField is null)
            {
                Mod.Instance.Log.Error("Unable to get LeveldownQueue FieldInfo");
                Enabled = false;
                return;
            }

            _leveldownQueue = (NativeQueue<Entity>)m_LeveldownQueueField.GetValue(World.GetOrCreateSystemManaged<PropertyRenterSystem>());

            // Set state from current settings.
            if (Mod.Instance.ActiveSettings is ModSettings activeSettings)
            {
                DisableLevelling = activeSettings.DisableLevelling;
                DisableAbandonment = activeSettings.NoAbandonment;
            }
        }

        /// <summary>
        /// Called when the game is loaded.
        /// </summary>
        /// <param name="serializationContext">Serialization context.</param>
        protected override void OnGameLoaded(Context serializationContext)
        {
            Mod.Instance.Log.Info("OnGameLoaded");
            base.OnGameLoaded(serializationContext);

            // Check and apply workaround Harmony patches to the Land Value Overhaul mod, if present.
            Patcher.Instance.PatchLandValueOverhaul(World);
        }

        /// <summary>
        /// Called every update.
        /// </summary>
        protected override void OnUpdate()
        {
            // Clear queues if levelling is disabled.
            if (DisableLevelling)
            {
                _levelupQueue.Clear();
                _leveldownQueue.Clear();

                // Don't need to do anything else.
                return;
            }

            // Upgrade any buildings.
            if (_levelupQueue.Count != 0)
            {
                LevelupJob levelupJob = default;
                levelupJob.m_LevelLockedData = SystemAPI.GetComponentLookup<LevelLocked>(true);
                levelupJob.m_EntityType = SystemAPI.GetEntityTypeHandle();
                levelupJob.m_SpawnableBuildingType = SystemAPI.GetComponentTypeHandle<SpawnableBuildingData>(true);
                levelupJob.m_BuildingType = SystemAPI.GetComponentTypeHandle<BuildingData>(true);
                levelupJob.m_BuildingPropertyType = SystemAPI.GetComponentTypeHandle<BuildingPropertyData>(true);
                levelupJob.m_ObjectGeometryType = SystemAPI.GetComponentTypeHandle<ObjectGeometryData>(true);
                levelupJob.m_BuildingSpawnGroupType = SystemAPI.GetSharedComponentTypeHandle<BuildingSpawnGroupData>();
                levelupJob.m_TransformData = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true);
                levelupJob.m_BlockData = SystemAPI.GetComponentLookup<Block>(true);
                levelupJob.m_ValidAreaData = SystemAPI.GetComponentLookup<ValidArea>(true);
                levelupJob.m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(true);
                levelupJob.m_SpawnableBuildings = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true);
                levelupJob.m_Buildings = SystemAPI.GetComponentLookup<BuildingData>(true);
                levelupJob.m_BuildingPropertyDatas = SystemAPI.GetComponentLookup<BuildingPropertyData>(true);
                levelupJob.m_OfficeBuilding = SystemAPI.GetComponentLookup<OfficeBuilding>(true);
                levelupJob.m_ZoneData = SystemAPI.GetComponentLookup<ZoneData>(true);
                levelupJob.m_Cells = SystemAPI.GetBufferLookup<Cell>(true);
                levelupJob.m_BuildingConfigurationData = _buildingSettingsQuery.GetSingleton<BuildingConfigurationData>();
                levelupJob.m_SpawnableBuildingChunks = _buildingPrefabGroupQuery.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator, out _);
                levelupJob.m_ZoneSearchTree = _zoneSearchSystem.GetSearchTree(readOnly: true, out _);
                levelupJob.m_RandomSeed = RandomSeed.Next();
                levelupJob.m_IconCommandBuffer = _iconCommandSystem.CreateCommandBuffer();
                levelupJob.m_LevelupQueue = _levelupQueue;
                levelupJob.m_CommandBuffer = _endFrameBarrier.CreateCommandBuffer();
                levelupJob.m_TriggerBuffer = _triggerSystem.CreateActionBuffer();
                levelupJob.m_ZoneBuiltLevelQueue = _zoneBuiltRequirementSystem.GetZoneBuiltLevelQueue(out _);
                JobHandle jobHandle = IJobExtensions.Schedule(levelupJob, Dependency);
                _zoneSearchSystem.AddSearchTreeReader(jobHandle);
                _zoneBuiltRequirementSystem.AddWriter(jobHandle);
                _endFrameBarrier.AddJobHandleForProducer(jobHandle);
                _triggerSystem.AddActionBufferWriter(jobHandle);
                Dependency = jobHandle;
            }

            // Downgrade any buildings.
            if (_leveldownQueue.Count != 0)
            {
                {
                    LeveldownJob leveldownJob = default;
                    leveldownJob.m_DisableAbandonment = DisableAbandonment;
                    leveldownJob.m_LevelLockedData = SystemAPI.GetComponentLookup<LevelLocked>(true);
                    leveldownJob.m_BuildingDatas = __TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup;
                    leveldownJob.m_Prefabs = __TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup;
                    leveldownJob.m_SpawnableBuildings = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true);
                    leveldownJob.m_Buildings = SystemAPI.GetComponentLookup<Building>(false);
                    leveldownJob.m_ElectricityConsumers = SystemAPI.GetComponentLookup<ElectricityConsumer>(true);
                    leveldownJob.m_GarbageProducers = SystemAPI.GetComponentLookup<GarbageProducer>(true);
                    leveldownJob.m_GroundPolluters = SystemAPI.GetComponentLookup<GroundPolluter>(true);
                    leveldownJob.m_MailProducers = SystemAPI.GetComponentLookup<MailProducer>(true);
                    leveldownJob.m_WaterConsumers = SystemAPI.GetComponentLookup<WaterConsumer>(true);
                    leveldownJob.m_BuildingPropertyDatas = __TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup;
                    leveldownJob.m_OfficeBuilding = __TypeHandle.__Game_Prefabs_OfficeBuilding_RO_ComponentLookup;
                    leveldownJob.m_TriggerBuffer = _triggerSystem.CreateActionBuffer();
                    leveldownJob.m_CrimeProducers = SystemAPI.GetComponentLookup<CrimeProducer>(false);
                    leveldownJob.m_Renters = SystemAPI.GetBufferLookup<Renter>(false);
                    leveldownJob.m_BuildingConfigurationData = _buildingSettingsQuery.GetSingleton<BuildingConfigurationData>();
                    leveldownJob.m_LeveldownQueue = _leveldownQueue;
                    leveldownJob.m_CommandBuffer = _endFrameBarrier.CreateCommandBuffer();
                    leveldownJob.m_UpdatedElectricityRoadEdges = _electricityRoadConnectionGraphSystem.GetEdgeUpdateQueue(out _);
                    leveldownJob.m_UpdatedWaterPipeRoadEdges = _waterPipeRoadConnectionGraphSystem.GetEdgeUpdateQueue(out _);
                    leveldownJob.m_IconCommandBuffer = _iconCommandSystem.CreateCommandBuffer();
                    leveldownJob.m_SimulationFrame = _simulationSystem.frameIndex;
                    JobHandle jobHandle = IJobExtensions.Schedule(leveldownJob, Dependency);
                    _endFrameBarrier.AddJobHandleForProducer(jobHandle);
                    _electricityRoadConnectionGraphSystem.AddQueueWriter(jobHandle);
                    _iconCommandSystem.AddCommandBufferWriter(jobHandle);
                    _triggerSystem.AddActionBufferWriter(jobHandle);
                    Dependency = jobHandle;
                }
            }

            return;
        }

        /// <summary>
        /// Called when the system is destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            Instance = null;

            // The level up and level down queues belong to PropertyRenterSystem, so we don't dispose of them here.
            base.OnDestroy();
        }

        /// <summary>
        /// Job to level up buildings.
        /// Derived from game code.
        /// </summary>
        [BurstCompile]
        private struct LevelupJob : IJob
        {
            [ReadOnly]
            public ComponentLookup<LevelLocked> m_LevelLockedData;
            [ReadOnly]
            public EntityTypeHandle m_EntityType;
            [ReadOnly]
            public ComponentTypeHandle<SpawnableBuildingData> m_SpawnableBuildingType;
            [ReadOnly]
            public ComponentTypeHandle<BuildingData> m_BuildingType;
            [ReadOnly]
            public ComponentTypeHandle<BuildingPropertyData> m_BuildingPropertyType;
            [ReadOnly]
            public ComponentTypeHandle<ObjectGeometryData> m_ObjectGeometryType;
            [ReadOnly]
            public SharedComponentTypeHandle<BuildingSpawnGroupData> m_BuildingSpawnGroupType;
            [ReadOnly]
            public ComponentLookup<Game.Objects.Transform> m_TransformData;
            [ReadOnly]
            public ComponentLookup<Block> m_BlockData;
            [ReadOnly]
            public ComponentLookup<ValidArea> m_ValidAreaData;
            [ReadOnly]
            public ComponentLookup<PrefabRef> m_Prefabs;
            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings;
            [ReadOnly]
            public ComponentLookup<BuildingData> m_Buildings;
            [ReadOnly]
            public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas;
            [ReadOnly]
            public ComponentLookup<OfficeBuilding> m_OfficeBuilding;
            [ReadOnly]
            public ComponentLookup<ZoneData> m_ZoneData;
            [ReadOnly]
            public BufferLookup<Cell> m_Cells;
            public BuildingConfigurationData m_BuildingConfigurationData;
            [ReadOnly]
            public NativeList<ArchetypeChunk> m_SpawnableBuildingChunks;
            [ReadOnly]
            public NativeQuadTree<Entity, Bounds2> m_ZoneSearchTree;
            [ReadOnly]
            public RandomSeed m_RandomSeed;
            public IconCommandBuffer m_IconCommandBuffer;
            public NativeQueue<Entity> m_LevelupQueue;
            public EntityCommandBuffer m_CommandBuffer;
            public NativeQueue<TriggerAction> m_TriggerBuffer;
            public NativeQueue<ZoneBuiltLevelUpdate> m_ZoneBuiltLevelQueue;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute()
            {
                Random random = m_RandomSeed.GetRandom(0);
                while (m_LevelupQueue.TryDequeue(out Entity item))
                {
                    Entity prefab = m_Prefabs[item].m_Prefab;
                    if (!m_SpawnableBuildings.HasComponent(prefab))
                    {
                        continue;
                    }

                    // Exempt level-locked buildings.
                    if (m_LevelLockedData.HasComponent(item))
                    {
                        continue;
                    }

                    SpawnableBuildingData spawnableBuildingData = m_SpawnableBuildings[prefab];
                    BuildingData prefabBuildingData = m_Buildings[prefab];
                    BuildingPropertyData buildingPropertyData = m_BuildingPropertyDatas[prefab];
                    ZoneData zoneData = m_ZoneData[spawnableBuildingData.m_ZonePrefab];
                    float maxHeight = GetMaxHeight(item, prefabBuildingData);
                    Entity entity = SelectSpawnableBuilding(zoneData.m_ZoneType, spawnableBuildingData.m_Level + 1, prefabBuildingData.m_LotSize, maxHeight, prefabBuildingData.m_Flags & (Game.Prefabs.BuildingFlags.LeftAccess | Game.Prefabs.BuildingFlags.RightAccess), buildingPropertyData, ref random);

                    if (entity == Entity.Null)
                    {
                        continue;
                    }

                    m_CommandBuffer.AddComponent(item, new UnderConstruction
                    {
                        m_NewPrefab = entity,
                        m_Progress = byte.MaxValue,
                    });

                    if (buildingPropertyData.CountProperties(AreaType.Residential) > 0)
                    {
                        m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpResidentialBuilding, Entity.Null, item, item));
                    }

                    if (buildingPropertyData.CountProperties(AreaType.Commercial) > 0)
                    {
                        m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpCommercialBuilding, Entity.Null, item, item));
                    }

                    if (buildingPropertyData.CountProperties(AreaType.Industrial) > 0)
                    {
                        if (m_OfficeBuilding.HasComponent(prefab))
                        {
                            m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpOfficeBuilding, Entity.Null, item, item));
                        }
                        else
                        {
                            m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelUpIndustrialBuilding, Entity.Null, item, item));
                        }
                    }

                    m_ZoneBuiltLevelQueue.Enqueue(new ZoneBuiltLevelUpdate
                    {
                        m_Zone = spawnableBuildingData.m_ZonePrefab,
                        m_FromLevel = spawnableBuildingData.m_Level,
                        m_ToLevel = spawnableBuildingData.m_Level + 1,
                        m_Squares = prefabBuildingData.m_LotSize.x * prefabBuildingData.m_LotSize.y,
                    });

                    m_IconCommandBuffer.Add(item, m_BuildingConfigurationData.m_LevelUpNotification, IconPriority.Info, IconClusterLayer.Transaction);
                }
            }

            /// <summary>
            /// Selects a building to spawn.
            /// </summary>
            /// <param name="zoneType">Zone type.</param>
            /// <param name="level">Target building level.</param>
            /// <param name="lotSize">Lot size.</param>
            /// <param name="maxHeight">Building maximum height.</param>
            /// <param name="accessFlags">Building access flags.</param>
            /// <param name="buildingPropertyData">Building property data.</param>
            /// <param name="random">Random struct.</param>
            /// <returns>Selected building entity.</returns>
            private Entity SelectSpawnableBuilding(ZoneType zoneType, int level, int2 lotSize, float maxHeight, BuildingFlags accessFlags, BuildingPropertyData buildingPropertyData, ref Random random)
            {
                int num = 0;
                Entity result = Entity.Null;
                for (int i = 0; i < m_SpawnableBuildingChunks.Length; i++)
                {
                    ArchetypeChunk archetypeChunk = m_SpawnableBuildingChunks[i];
                    if (!archetypeChunk.GetSharedComponent(m_BuildingSpawnGroupType).m_ZoneType.Equals(zoneType))
                    {
                        continue;
                    }

                    NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(m_EntityType);
                    NativeArray<SpawnableBuildingData> nativeArray2 = archetypeChunk.GetNativeArray(ref m_SpawnableBuildingType);
                    NativeArray<BuildingData> nativeArray3 = archetypeChunk.GetNativeArray(ref m_BuildingType);
                    NativeArray<BuildingPropertyData> nativeArray4 = archetypeChunk.GetNativeArray(ref m_BuildingPropertyType);
                    NativeArray<ObjectGeometryData> nativeArray5 = archetypeChunk.GetNativeArray(ref m_ObjectGeometryType);
                    for (int j = 0; j < archetypeChunk.Count; j++)
                    {
                        SpawnableBuildingData spawnableBuildingData = nativeArray2[j];
                        BuildingData buildingData = nativeArray3[j];
                        BuildingPropertyData buildingPropertyData2 = nativeArray4[j];
                        ObjectGeometryData objectGeometryData = nativeArray5[j];
                        if (level == spawnableBuildingData.m_Level && lotSize.Equals(buildingData.m_LotSize) && objectGeometryData.m_Size.y <= maxHeight && (buildingData.m_Flags & (BuildingFlags.LeftAccess | BuildingFlags.RightAccess)) == accessFlags && buildingPropertyData.m_ResidentialProperties <= buildingPropertyData2.m_ResidentialProperties && buildingPropertyData.m_AllowedManufactured == buildingPropertyData2.m_AllowedManufactured && buildingPropertyData.m_AllowedSold == buildingPropertyData2.m_AllowedSold && buildingPropertyData.m_AllowedStored == buildingPropertyData2.m_AllowedStored)
                        {
                            int num2 = 100;
                            num += num2;
                            if (random.NextInt(num) < num2)
                            {
                                result = nativeArray[j];
                            }
                        }
                    }
                }

                return result;
            }

            /// <summary>
            /// Gets a building's maximum height.
            /// </summary>
            /// <param name="building">Building entity.</param>
            /// <param name="prefabBuildingData">Building prefab data.</param>
            /// <returns>Building maximum height, in metres.</returns>
            private float GetMaxHeight(Entity building, BuildingData prefabBuildingData)
            {
                Game.Objects.Transform transform = m_TransformData[building];
                float2 xz = math.rotate(transform.m_Rotation, new float3(8f, 0f, 0f)).xz;
                float2 xz2 = math.rotate(transform.m_Rotation, new float3(0f, 0f, 8f)).xz;
                float2 @float = xz * ((prefabBuildingData.m_LotSize.x * 0.5f) - 0.5f);
                float2 float2 = xz2 * ((prefabBuildingData.m_LotSize.y * 0.5f) - 0.5f);
                float2 float3 = math.abs(float2) + math.abs(@float);
                Iterator iterator = default;
                iterator.m_Bounds = new Bounds2(transform.m_Position.xz - float3, transform.m_Position.xz + float3);
                iterator.m_LotSize = prefabBuildingData.m_LotSize;
                iterator.m_StartPosition = transform.m_Position.xz + float2 + @float;
                iterator.m_Right = xz;
                iterator.m_Forward = xz2;
                iterator.m_MaxHeight = int.MaxValue;
                iterator.m_BlockData = m_BlockData;
                iterator.m_ValidAreaData = m_ValidAreaData;
                iterator.m_Cells = m_Cells;
                m_ZoneSearchTree.Iterate(ref iterator);
                return iterator.m_MaxHeight - transform.m_Position.y;
            }

            /// <summary>
            /// Zone search tree iterator.
            /// </summary>
            private struct Iterator : INativeQuadTreeIterator<Entity, Bounds2>, IUnsafeQuadTreeIterator<Entity, Bounds2>
            {
                public Bounds2 m_Bounds;
                public int2 m_LotSize;
                public float2 m_StartPosition;
                public float2 m_Right;
                public float2 m_Forward;
                public int m_MaxHeight;
                public ComponentLookup<Block> m_BlockData;
                public ComponentLookup<ValidArea> m_ValidAreaData;
                public BufferLookup<Cell> m_Cells;

                public readonly bool Intersect(Bounds2 bounds)
                {
                    return MathUtils.Intersect(bounds, m_Bounds);
                }

                public void Iterate(Bounds2 bounds, Entity blockEntity)
                {
                    if (!MathUtils.Intersect(bounds, m_Bounds))
                    {
                        return;
                    }

                    ValidArea validArea = m_ValidAreaData[blockEntity];
                    if (validArea.m_Area.y <= validArea.m_Area.x)
                    {
                        return;
                    }

                    Block block = m_BlockData[blockEntity];
                    DynamicBuffer<Cell> dynamicBuffer = m_Cells[blockEntity];
                    float2 startPosition = m_StartPosition;
                    int2 @int = default;
                    @int.y = 0;
                    while (@int.y < m_LotSize.y)
                    {
                        float2 position = startPosition;
                        @int.x = 0;
                        while (@int.x < m_LotSize.x)
                        {
                            int2 cellIndex = ZoneUtils.GetCellIndex(block, position);
                            if (math.all((cellIndex >= validArea.m_Area.xz) & (cellIndex < validArea.m_Area.yw)))
                            {
                                int index = (cellIndex.y * block.m_Size.x) + cellIndex.x;
                                Cell cell = dynamicBuffer[index];
                                if ((cell.m_State & CellFlags.Visible) != 0)
                                {
                                    m_MaxHeight = math.min(m_MaxHeight, cell.m_Height);
                                }
                            }

                            position -= m_Right;
                            @int.x++;
                        }

                        startPosition -= m_Forward;
                        @int.y++;
                    }
                }
            }
        }

        /// <summary>
        /// Job to level down buildings.
        /// Derived from game code.
        /// </summary>
        [BurstCompile]
        private struct LeveldownJob : IJob
        {
            [ReadOnly]
            public bool m_DisableAbandonment;
            [ReadOnly]
            public ComponentLookup<LevelLocked> m_LevelLockedData;
            [ReadOnly]
            public ComponentLookup<PrefabRef> m_Prefabs;
            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings;
            [ReadOnly]
            public ComponentLookup<BuildingData> m_BuildingDatas;
            [ReadOnly]
            public ComponentLookup<Building> m_Buildings;
            [ReadOnly]
            public ComponentLookup<ElectricityConsumer> m_ElectricityConsumers;
            [ReadOnly]
            public ComponentLookup<WaterConsumer> m_WaterConsumers;
            [ReadOnly]
            public ComponentLookup<GarbageProducer> m_GarbageProducers;
            [ReadOnly]
            public ComponentLookup<GroundPolluter> m_GroundPolluters;
            [ReadOnly]
            public ComponentLookup<MailProducer> m_MailProducers;
            [ReadOnly]
            public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas;
            [ReadOnly]
            public ComponentLookup<OfficeBuilding> m_OfficeBuilding;
            public NativeQueue<TriggerAction> m_TriggerBuffer;
            public ComponentLookup<CrimeProducer> m_CrimeProducers;
            public BufferLookup<Renter> m_Renters;
            [ReadOnly]
            public BuildingConfigurationData m_BuildingConfigurationData;
            public NativeQueue<Entity> m_LeveldownQueue;
            public EntityCommandBuffer m_CommandBuffer;
            public NativeQueue<Entity> m_UpdatedElectricityRoadEdges;
            public NativeQueue<Entity> m_UpdatedWaterPipeRoadEdges;
            public IconCommandBuffer m_IconCommandBuffer;
            public uint m_SimulationFrame;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute()
            {
                while (m_LeveldownQueue.TryDequeue(out Entity item))
                {
                    if (!m_Prefabs.HasComponent(item))
                    {
                        continue;
                    }

                    // Exempt level-locked buildings, or any buildings if abandonment is disabled.
                    if (m_DisableAbandonment || m_LevelLockedData.HasComponent(item))
                    {
                        m_CommandBuffer.RemoveComponent<PropertyOnMarket>(item);
                        m_CommandBuffer.AddComponent<PropertyToBeOnMarket>(item);
                        continue;
                    }

                    Entity prefab = m_Prefabs[item].m_Prefab;
                    if (!m_SpawnableBuildings.HasComponent(prefab))
                    {
                        continue;
                    }

                    BuildingPropertyData buildingPropertyData = m_BuildingPropertyDatas[prefab];
                    m_CommandBuffer.AddComponent(item, new Abandoned
                    {
                        m_AbandonmentTime = m_SimulationFrame,
                    });

                    m_CommandBuffer.AddComponent(item, default(Updated));
                    if (m_ElectricityConsumers.HasComponent(item))
                    {
                        m_CommandBuffer.RemoveComponent<ElectricityConsumer>(item);
                        Entity roadEdge = m_Buildings[item].m_RoadEdge;
                        if (roadEdge != Entity.Null)
                        {
                            m_UpdatedElectricityRoadEdges.Enqueue(roadEdge);
                        }
                    }

                    if (m_WaterConsumers.HasComponent(item))
                    {
                        m_CommandBuffer.RemoveComponent<WaterConsumer>(item);
                        Entity roadEdge2 = m_Buildings[item].m_RoadEdge;
                        if (roadEdge2 != Entity.Null)
                        {
                            m_UpdatedWaterPipeRoadEdges.Enqueue(roadEdge2);
                        }
                    }

                    if (m_GarbageProducers.HasComponent(item))
                    {
                        m_CommandBuffer.RemoveComponent<GarbageProducer>(item);
                    }

                    if (m_GroundPolluters.HasComponent(item))
                    {
                        m_CommandBuffer.RemoveComponent<GroundPolluter>(item);
                    }

                    if (m_MailProducers.HasComponent(item))
                    {
                        m_CommandBuffer.RemoveComponent<MailProducer>(item);
                    }

                    if (m_CrimeProducers.HasComponent(item))
                    {
                        CrimeProducer crimeProducer = m_CrimeProducers[item];
                        m_CommandBuffer.SetComponent(item, new CrimeProducer
                        {
                            m_Crime = crimeProducer.m_Crime * 2f,
                            m_PatrolRequest = crimeProducer.m_PatrolRequest,
                        });
                    }

                    if (m_Renters.HasBuffer(item))
                    {
                        DynamicBuffer<Renter> dynamicBuffer = m_Renters[item];
                        for (int num = dynamicBuffer.Length - 1; num >= 0; num--)
                        {
                            m_CommandBuffer.RemoveComponent<PropertyRenter>(dynamicBuffer[num].m_Renter);
                            dynamicBuffer.RemoveAt(num);
                        }
                    }

                    if ((m_Buildings[item].m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) != 0)
                    {
                        Building value = m_Buildings[item];
                        m_IconCommandBuffer.Remove(item, m_BuildingConfigurationData.m_HighRentNotification);
                        value.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
                        m_Buildings[item] = value;
                    }

                    m_IconCommandBuffer.Remove(item, IconPriority.Problem);
                    m_IconCommandBuffer.Remove(item, IconPriority.FatalProblem);
                    m_IconCommandBuffer.Add(item, m_BuildingConfigurationData.m_AbandonedNotification, IconPriority.FatalProblem);
                    if (buildingPropertyData.CountProperties(AreaType.Commercial) > 0)
                    {
                        m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelDownCommercialBuilding, Entity.Null, item, item));
                    }

                    if (buildingPropertyData.CountProperties(AreaType.Industrial) > 0)
                    {
                        if (m_OfficeBuilding.HasComponent(prefab))
                        {
                            m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelDownOfficeBuilding, Entity.Null, item, item));
                        }
                        else
                        {
                            m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.LevelDownIndustrialBuilding, Entity.Null, item, item));
                        }
                    }
                }
            }
        }
    }
}
