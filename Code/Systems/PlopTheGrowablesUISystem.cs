// <copyright file="PlopTheGrowablesUISystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using Colossal.Logging;
    using Colossal.UI.Binding;
    using Game.UI;
    using Unity.Entities;

    /// <summary>
    /// UI handling system for Plop the Growables.
    /// </summary>
    public sealed partial class PlopTheGrowablesUISystem : UISystemBase
    {
        private ILog _log;

        private Entity _selectedEntity;

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            // Set log.
            _log = Mod.Instance.Log;

            // Apply lock level UI bindings.
            AddUpdateBinding(new GetterValueBinding<bool>("PlopTheGrowables", "IsBuildingLocked", () => EntityManager.HasComponent<LevelLocked>(_selectedEntity)));
            AddBinding(new TriggerBinding<Entity>("PlopTheGrowables", "SelectedEntity", (Entity entity) => _selectedEntity = entity));
            AddBinding(new TriggerBinding<Entity>("PlopTheGrowables", "ToggleLockLevel", ToggleLevelLock));
        }

        /// <summary>
        /// Toggles the level locked status of the given entity.
        /// </summary>
        /// <param name="entity">Entity to toggle.</param>
        private void ToggleLevelLock(Entity entity)
        {
            // Ensure that we have the correct entity set.
            if (_selectedEntity != entity)
            {
                _log.Info($"Updating selected entity to {entity} when toggling locked status");
                _selectedEntity = entity;
            }

            // Toggle locked status by adding/removing the locking component.
            if (_selectedEntity != Entity.Null)
            {
                if (EntityManager.HasComponent<LevelLocked>(entity))
                {
                    EntityManager.RemoveComponent<LevelLocked>(entity);
                }
                else
                {
                    EntityManager.AddComponent<LevelLocked>(entity);
                }
            }
        }
    }
}