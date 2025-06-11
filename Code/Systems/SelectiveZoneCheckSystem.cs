// <copyright file="SelectiveZoneCheckSystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>
namespace PlopTheGrowables
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    /// Custom building zone check system to implement optional de-spawning if zoning underneath growable buildings changes.
    /// </summary>
    public partial class SelectiveZoneCheckSystem : GameSystemBase
    {
        private Game.Zones.UpdateCollectSystem _zoneUpdateCollectSystem;
        private Game.Zones.SearchSystem _zoneSearchSystem;
        private ModificationEndBarrier _modificationEndBarrier;
        private Game.Objects.SearchSystem _objectSearchSystem;
        private ToolSystem _toolSystem;
        private IconCommandSystem _iconCommandSystem;
        private EntityQuery _buildingSettingsQuery;

        /// <summary>
        /// Gets the active instance.
        /// </summary>
        public static SelectiveZoneCheckSystem Instance { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether spawned buildings will be de-spawned if their underlying zoning changes.
        /// </summary>
        public bool SpawnedZoneDespawn { get; set; } = false;

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            Instance = this;

            base.OnCreate();

            // Set references.
            _zoneUpdateCollectSystem = World.GetOrCreateSystemManaged<Game.Zones.UpdateCollectSystem>();
            _zoneSearchSystem = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            _modificationEndBarrier = World.GetOrCreateSystemManaged<ModificationEndBarrier>();
            _objectSearchSystem = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            _iconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
            _buildingSettingsQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());

            // Set state from current settings.
            if (Mod.Instance.ActiveSettings is ModSettings activeSettings)
            {
                SpawnedZoneDespawn = activeSettings.SpawnedZoneDespawn;
            }
        }

        /// <summary>
        /// Called every update.
        /// </summary>
        protected override void OnUpdate()
        {
            // Don't do anything unless we're set to selectively despawn and queries are ready.
            if (SpawnedZoneDespawn && _zoneUpdateCollectSystem.isUpdated && !_buildingSettingsQuery.IsEmptyIgnoreFilter)
            {
                // Entity collections.
                NativeQueue<Entity> queue = new (Allocator.TempJob);
                NativeList<Entity> list = new (Allocator.TempJob);
                NativeList<Bounds2> updatedBounds = _zoneUpdateCollectSystem.GetUpdatedBounds(readOnly: true, out JobHandle dependencies);

                // Find spawnable buildings job.
                FindSpawnableBuildingsJob findSpawnableBuildingsJob = default;
                findSpawnableBuildingsJob.m_Bounds = updatedBounds.AsDeferredJobArray();
                findSpawnableBuildingsJob.m_SearchTree = _objectSearchSystem.GetStaticSearchTree(readOnly: true, out var dependencies2);
                findSpawnableBuildingsJob.m_BuildingData = SystemAPI.GetComponentLookup<Building>(true);
                findSpawnableBuildingsJob.m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true);
                findSpawnableBuildingsJob.m_PrefabSpawnableBuildingData = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true);
                findSpawnableBuildingsJob.m_PrefabSignatureBuildingData = SystemAPI.GetComponentLookup<SignatureBuildingData>(true);
                findSpawnableBuildingsJob.m_ResultQueue = queue.AsParallelWriter();

                // Collect entities job.
                CollectEntitiesJob collectEntitiesJob = default;
                collectEntitiesJob.m_Queue = queue;
                collectEntitiesJob.m_List = list;

                // Check building zones job.
                CheckBuildingZonesJob checkBuildingZonesJob = default;
                checkBuildingZonesJob.m_PloppedBuildingData = SystemAPI.GetComponentLookup<PloppedBuilding>(true);
                checkBuildingZonesJob.m_CondemnedData = SystemAPI.GetComponentLookup<Condemned>(true);
                checkBuildingZonesJob.m_BlockData = SystemAPI.GetComponentLookup<Block>(true);
                checkBuildingZonesJob.m_ValidAreaData = SystemAPI.GetComponentLookup<ValidArea>(true);
                checkBuildingZonesJob.m_DestroyedData = SystemAPI.GetComponentLookup<Destroyed>(true);
                checkBuildingZonesJob.m_AbandonedData = SystemAPI.GetComponentLookup<Abandoned>(true);
                checkBuildingZonesJob.m_TransformData = SystemAPI.GetComponentLookup<Transform>(true);
                checkBuildingZonesJob.m_AttachedData = SystemAPI.GetComponentLookup<Attached>(true);
                checkBuildingZonesJob.m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true);
                checkBuildingZonesJob.m_PrefabData = SystemAPI.GetComponentLookup<PrefabData>(true);
                checkBuildingZonesJob.m_PrefabBuildingData = SystemAPI.GetComponentLookup<BuildingData>(true);
                checkBuildingZonesJob.m_PrefabSpawnableBuildingData = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true);
                checkBuildingZonesJob.m_PrefabPlaceholderBuildingData = SystemAPI.GetComponentLookup<PlaceholderBuildingData>(true);
                checkBuildingZonesJob.m_PrefabZoneData = SystemAPI.GetComponentLookup<ZoneData>(true);
                checkBuildingZonesJob.m_Cells = SystemAPI.GetBufferLookup<Cell>(true);
                checkBuildingZonesJob.m_BuildingConfigurationData = _buildingSettingsQuery.GetSingleton<BuildingConfigurationData>();
                checkBuildingZonesJob.m_Buildings = list.AsDeferredJobArray();
                checkBuildingZonesJob.m_SearchTree = _zoneSearchSystem.GetSearchTree(readOnly: true, out var dependencies3);
                checkBuildingZonesJob.m_EditorMode = _toolSystem.actionMode.IsEditor();
                checkBuildingZonesJob.m_IconCommandBuffer = _iconCommandSystem.CreateCommandBuffer();
                checkBuildingZonesJob.m_CommandBuffer = _modificationEndBarrier.CreateCommandBuffer().AsParallelWriter();

                // Schedule jobs.
                JobHandle jobHandle = findSpawnableBuildingsJob.Schedule(updatedBounds, 1, JobHandle.CombineDependencies(Dependency, dependencies, dependencies2));
                JobHandle jobHandle2 = IJobExtensions.Schedule(collectEntitiesJob, jobHandle);
                JobHandle jobHandle3 = checkBuildingZonesJob.Schedule(list, 1, JobHandle.CombineDependencies(jobHandle2, dependencies3));

                // Clean up after ourselves.
                queue.Dispose(jobHandle2);
                list.Dispose(jobHandle3);

                // Apply jobs.
                _zoneUpdateCollectSystem.AddBoundsReader(jobHandle);
                _objectSearchSystem.AddStaticSearchTreeReader(jobHandle);
                _zoneSearchSystem.AddSearchTreeReader(jobHandle3);
                _iconCommandSystem.AddCommandBufferWriter(jobHandle3);
                _modificationEndBarrier.AddJobHandleForProducer(jobHandle3);
                Dependency = jobHandle3;
            }
        }

        /// <summary>
        /// Job to find spawnable buildings.
        /// Derived from game code.
        /// </summary>
        [BurstCompile]
        private struct FindSpawnableBuildingsJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeArray<Bounds2> m_Bounds;
            [ReadOnly]
            public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_SearchTree;
            [ReadOnly]
            public ComponentLookup<Building> m_BuildingData;
            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefData;
            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> m_PrefabSpawnableBuildingData;
            [ReadOnly]
            public ComponentLookup<SignatureBuildingData> m_PrefabSignatureBuildingData;
            public NativeQueue<Entity>.ParallelWriter m_ResultQueue;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(int index)
            {
                Iterator iterator = default;
                iterator.m_Bounds = m_Bounds[index];
                iterator.m_ResultQueue = m_ResultQueue;
                iterator.m_BuildingData = m_BuildingData;
                iterator.m_PrefabRefData = m_PrefabRefData;
                iterator.m_PrefabSpawnableBuildingData = m_PrefabSpawnableBuildingData;
                iterator.m_PrefabSignatureBuildingData = m_PrefabSignatureBuildingData;
                m_SearchTree.Iterate(ref iterator);
            }

            /// <summary>
            /// Job iteration.
            /// </summary>
            private struct Iterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
            {
                public Bounds2 m_Bounds;
                public NativeQueue<Entity>.ParallelWriter m_ResultQueue;
                public ComponentLookup<Building> m_BuildingData;
                public ComponentLookup<PrefabRef> m_PrefabRefData;
                public ComponentLookup<SpawnableBuildingData> m_PrefabSpawnableBuildingData;
                public ComponentLookup<SignatureBuildingData> m_PrefabSignatureBuildingData;

                /// <summary>
                /// Tests whether XZ bounds intersect.
                /// </summary>
                /// <param name="bounds">Quad tree bounds (XZ) for testing.</param>
                /// <returns><c>true</c> if bounds intersect, <c>false</c> otherwise.</returns>
                public readonly bool Intersect(QuadTreeBoundsXZ bounds)
                {
                    return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds);
                }

                /// <summary>
                /// Job iterator.
                /// </summary>
                /// <param name="bounds">Bounds quad tree to check for intersection.</param>
                /// <param name="objectEntity">Entity to enqueue if bounds intersect.</param>
                public void Iterate(QuadTreeBoundsXZ bounds, Entity objectEntity)
                {
                    if (MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds) && m_BuildingData.HasComponent(objectEntity))
                    {
                        PrefabRef prefabRef = m_PrefabRefData[objectEntity];
                        if (m_PrefabSpawnableBuildingData.HasComponent(prefabRef.m_Prefab) && !m_PrefabSignatureBuildingData.HasComponent(prefabRef.m_Prefab))
                        {
                            m_ResultQueue.Enqueue(objectEntity);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Job to collect entities for building zone checking.
        /// Derived from game code.
        /// </summary>
        [BurstCompile]
        private struct CollectEntitiesJob : IJob
        {
            /// <summary>
            /// Entity queue (entities to process).
            /// </summary>
            public NativeQueue<Entity> m_Queue;

            /// <summary>
            /// Entity list (outgoing list).
            /// </summary>
            public NativeList<Entity> m_List;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute()
            {
                int count = m_Queue.Count;
                if (count == 0)
                {
                    return;
                }

                m_List.ResizeUninitialized(count);
                for (int i = 0; i < count; i++)
                {
                    m_List[i] = m_Queue.Dequeue();
                }

                m_List.Sort(default(EntityComparer));
                Entity entity = Entity.Null;
                int num = 0;
                int num2 = 0;
                while (num < m_List.Length)
                {
                    Entity entity2 = m_List[num++];
                    if (entity2 != entity)
                    {
                        m_List[num2++] = entity2;
                        entity = entity2;
                    }
                }

                if (num2 < m_List.Length)
                {
                    m_List.RemoveRangeSwapBack(num2, m_List.Length - num2);
                }
            }

            /// <summary>
            /// Entity comparison.
            /// </summary>
            [StructLayout(LayoutKind.Sequential, Size = 1)]
            private struct EntityComparer : IComparer<Entity>
            {
                public readonly int Compare(Entity x, Entity y)
                {
                    return x.Index - y.Index;
                }
            }
        }

        /// <summary>
        /// Job to check building zones.
        /// Derived from game code.
        /// </summary>
        [BurstCompile]
        private struct CheckBuildingZonesJob : IJobParallelForDefer
        {
            [ReadOnly]
            public ComponentLookup<PloppedBuilding> m_PloppedBuildingData;
            [ReadOnly]
            public ComponentLookup<Condemned> m_CondemnedData;
            [ReadOnly]
            public ComponentLookup<Block> m_BlockData;
            [ReadOnly]
            public ComponentLookup<ValidArea> m_ValidAreaData;
            [ReadOnly]
            public ComponentLookup<Destroyed> m_DestroyedData;
            [ReadOnly]
            public ComponentLookup<Abandoned> m_AbandonedData;
            [ReadOnly]
            public ComponentLookup<Transform> m_TransformData;
            [ReadOnly]
            public ComponentLookup<Attached> m_AttachedData;
            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefData;
            [ReadOnly]
            public ComponentLookup<PrefabData> m_PrefabData;
            [ReadOnly]
            public ComponentLookup<BuildingData> m_PrefabBuildingData;
            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> m_PrefabSpawnableBuildingData;
            [ReadOnly]
            public ComponentLookup<PlaceholderBuildingData> m_PrefabPlaceholderBuildingData;
            [ReadOnly]
            public ComponentLookup<ZoneData> m_PrefabZoneData;
            [ReadOnly]
            public BufferLookup<Cell> m_Cells;
            [ReadOnly]
            public BuildingConfigurationData m_BuildingConfigurationData;
            [ReadOnly]
            public NativeArray<Entity> m_Buildings;
            [ReadOnly]
            public NativeQuadTree<Entity, Bounds2> m_SearchTree;
            [ReadOnly]
            public bool m_EditorMode;
            public IconCommandBuffer m_IconCommandBuffer;
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            /// <summary>
            /// Job execution.
            /// </summary>
            /// <param name="index">Data index.</param>
            public void Execute(int index)
            {
                Entity entity = m_Buildings[index];

                // Ignore plopped buildings.
                if (m_PloppedBuildingData.HasComponent(entity))
                {
                    return;
                }

                PrefabRef prefabRef = m_PrefabRefData[entity];
                BuildingData prefabBuildingData = m_PrefabBuildingData[prefabRef.m_Prefab];
                SpawnableBuildingData prefabSpawnableBuildingData = m_PrefabSpawnableBuildingData[prefabRef.m_Prefab];
                bool isValid = m_EditorMode;
                if (!isValid)
                {
                    isValid = ValidateAttachedParent(entity, prefabBuildingData, prefabSpawnableBuildingData);
                }

                if (!isValid)
                {
                    isValid = ValidateZoneBlocks(entity, prefabBuildingData, prefabSpawnableBuildingData);
                }

                if (isValid)
                {
                    if (m_CondemnedData.HasComponent(entity))
                    {
                        m_CommandBuffer.RemoveComponent<Condemned>(index, entity);
                        m_IconCommandBuffer.Remove(entity, m_BuildingConfigurationData.m_CondemnedNotification);
                    }
                }
                else if (!m_CondemnedData.HasComponent(entity))
                {
                    m_CommandBuffer.AddComponent(index, entity, default(Condemned));
                    if (!m_DestroyedData.HasComponent(entity) && !m_AbandonedData.HasComponent(entity))
                    {
                        m_IconCommandBuffer.Add(entity, m_BuildingConfigurationData.m_CondemnedNotification, IconPriority.FatalProblem);
                    }
                }
            }

            /// <summary>
            /// Checks validation of the attached parent.
            /// </summary>
            /// <param name="building">Building to check.</param>
            /// <param name="prefabBuildingData">Building prefab.</param>
            /// <param name="prefabSpawnableBuildingData">Spawnable building data.</param>
            /// <returns><c>true</c> if validation is successful, <c>false</c> otherwise.</returns>
            private bool ValidateAttachedParent(Entity building, BuildingData prefabBuildingData, SpawnableBuildingData prefabSpawnableBuildingData)
            {
                if (m_AttachedData.HasComponent(building))
                {
                    Attached attached = m_AttachedData[building];
                    if (m_PrefabRefData.HasComponent(attached.m_Parent))
                    {
                        PrefabRef prefabRef = m_PrefabRefData[attached.m_Parent];
                        if (m_PrefabPlaceholderBuildingData.HasComponent(prefabRef.m_Prefab) && m_PrefabPlaceholderBuildingData[prefabRef.m_Prefab].m_ZonePrefab == prefabSpawnableBuildingData.m_ZonePrefab)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// Validates zone blocks for the given building.
            /// </summary>
            /// <param name="building">Building to check.</param>
            /// <param name="prefabBuildingData">Building prefab.</param>
            /// <param name="prefabSpawnableBuildingData">Spawnable building data.</param>
            /// <returns><c>true</c> if validation is successful, <c>false</c> otherwise.</returns>
            private bool ValidateZoneBlocks(Entity building, BuildingData prefabBuildingData, SpawnableBuildingData prefabSpawnableBuildingData)
            {
                Transform transform = m_TransformData[building];
                if (m_PrefabZoneData.TryGetComponent(prefabSpawnableBuildingData.m_ZonePrefab, out var componentData) && !componentData.m_ZoneType.Equals(ZoneType.None) && !m_PrefabData.IsComponentEnabled(prefabSpawnableBuildingData.m_ZonePrefab))
                {
                    return false;
                }

                float2 xz = math.rotate(transform.m_Rotation, new float3(8f, 0f, 0f)).xz;
                float2 xz2 = math.rotate(transform.m_Rotation, new float3(0f, 0f, 8f)).xz;
                float2 @float = xz * ((prefabBuildingData.m_LotSize.x * 0.5f) - 0.5f);
                float2 float2 = xz2 * ((prefabBuildingData.m_LotSize.y * 0.5f) - 0.5f);
                float2 float3 = math.abs(float2) + math.abs(@float);
                NativeArray<bool> validated = new (prefabBuildingData.m_LotSize.x * prefabBuildingData.m_LotSize.y, Allocator.Temp);
                Iterator iterator = default;
                iterator.m_Bounds = new Bounds2(transform.m_Position.xz - float3, transform.m_Position.xz + float3);
                iterator.m_LotSize = prefabBuildingData.m_LotSize;
                iterator.m_StartPosition = transform.m_Position.xz + float2 + @float;
                iterator.m_Right = xz;
                iterator.m_Forward = xz2;
                iterator.m_ZoneType = componentData.m_ZoneType;
                iterator.m_Validated = validated;
                iterator.m_BlockData = m_BlockData;
                iterator.m_ValidAreaData = m_ValidAreaData;
                iterator.m_Cells = m_Cells;
                Iterator iterator2 = iterator;
                m_SearchTree.Iterate(ref iterator2);
                bool isValid = (iterator2.m_Directions & CellFlags.Roadside) != 0;
                for (int i = 0; i < validated.Length; i++)
                {
                    isValid &= validated[i];
                }

                validated.Dispose();
                return isValid;
            }

            /// <summary>
            /// Job iteration.
            /// </summary>
            private struct Iterator : INativeQuadTreeIterator<Entity, Bounds2>, IUnsafeQuadTreeIterator<Entity, Bounds2>
            {
                public Bounds2 m_Bounds;
                public int2 m_LotSize;
                public float2 m_StartPosition;
                public float2 m_Right;
                public float2 m_Forward;
                public ZoneType m_ZoneType;
                public CellFlags m_Directions;
                public NativeArray<bool> m_Validated;
                public ComponentLookup<Block> m_BlockData;
                public ComponentLookup<ValidArea> m_ValidAreaData;
                public BufferLookup<Cell> m_Cells;

                /// <summary>
                /// Checks to see if the given bounds intersect with this job's bounds.
                /// </summary>
                /// <param name="bounds">Bounds to test.</param>
                /// <returns><c>true</c> if bounds intersect, <c>false</c> otherwise.</returns>
                public bool Intersect(Bounds2 bounds)
                {
                    return MathUtils.Intersect(bounds, m_Bounds);
                }

                /// <summary>
                /// Job iteration.
                /// </summary>
                /// <param name="bounds">Bounds to test.</param>
                /// <param name="blockEntity">Block entity.</param>
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

                    Block block = default;
                    block.m_Direction = m_Forward;
                    Block target = block;
                    Block block2 = m_BlockData[blockEntity];
                    DynamicBuffer<Cell> dynamicBuffer = m_Cells[blockEntity];
                    float2 startPosition = m_StartPosition;
                    int2 coordinates = default;
                    coordinates.y = 0;
                    while (coordinates.y < m_LotSize.y)
                    {
                        float2 position = startPosition;
                        coordinates.x = 0;
                        while (coordinates.x < m_LotSize.x)
                        {
                            int2 cellIndex = ZoneUtils.GetCellIndex(block2, position);
                            if (math.all((cellIndex >= validArea.m_Area.xz) & (cellIndex < validArea.m_Area.yw)))
                            {
                                int index = (cellIndex.y * block2.m_Size.x) + cellIndex.x;
                                Cell cell = dynamicBuffer[index];
                                if ((cell.m_State & CellFlags.Visible) != 0 && cell.m_Zone.Equals(m_ZoneType))
                                {
                                    m_Validated[(coordinates.y * m_LotSize.x) + coordinates.x] = true;
                                    if ((cell.m_State & (CellFlags.Roadside | CellFlags.RoadLeft | CellFlags.RoadRight | CellFlags.RoadBack)) != 0)
                                    {
                                        CellFlags roadDirection = ZoneUtils.GetRoadDirection(target, block2, cell.m_State);
                                        int4 x = math.select(trueValue: new int4(512, 4, 1024, 2048), falseValue: 0, test: new bool4(coordinates == 0, coordinates == m_LotSize - 1));
                                        m_Directions |= (CellFlags)((uint)roadDirection & (uint)(ushort)math.csum(x));
                                    }
                                }
                            }

                            position -= m_Right;
                            coordinates.x++;
                        }

                        startPosition -= m_Forward;
                        coordinates.y++;
                    }
                }
            }
        }
    }
}