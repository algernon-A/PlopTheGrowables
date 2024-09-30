// <copyright file="ModSettings.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using Colossal.IO.AssetDatabase;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.Settings;

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(Mod.ModName)]
    [SettingsUIShowGroupName(SpawnedBuildings, Locking, ApplyToExisting, DisableAll)]
    [SettingsUITabOrder(SpawnedBuildings, Locking, ApplyToExisting, DisableAll)]
    [SettingsUIGroupOrder(SpawnedBuildings, Locking, ApplyToExisting, DisableAll)]
    public class ModSettings : ModSetting
    {
        // String constants for categories.
        private const string SpawnedBuildings = "SpawnedBuildings";
        private const string Locking = "Locking";
        private const string ApplyToExisting = "ApplyToExisting";
        private const string DisableAll = "DisableAll";

        // Backing fields.
        private bool _disableLevelling = false;
        private bool _disableAbandonment = false;
        private bool _lockPloppedBuildings = false;
        private bool _spawnedZoneDespawn = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public ModSettings(IMod mod)
            : base(mod)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether spawned buildings should be affected by underlying zoning changes (e.g. de-spawn when the zoning is removed).
        /// </summary>
        [SettingsUISection(SpawnedBuildings)]
        public bool SpawnedZoneDespawn
        {
            get => _spawnedZoneDespawn;

            set
            {
                _spawnedZoneDespawn = value;

                // Update system, if it's ready.
                if (SelectiveZoneCheckSystem.Instance is SelectiveZoneCheckSystem selectiveZoneCheckSystem)
                {
                    selectiveZoneCheckSystem.SpawnedZoneDespawn = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether plopped buildings should be automatically level-locked on placement.
        /// </summary>
        [SettingsUISection(Locking)]
        public bool LockPloppedBuildings
        {
            get => _lockPloppedBuildings;

            set
            {
                _lockPloppedBuildings = value;

                // Update system, if it's ready.
                if (PloppedBuildingSystem.Instance is PloppedBuildingSystem ploppedBuildingSystem)
                {
                    ploppedBuildingSystem.LockPloppedBuildings = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether building abandonment should be disabled.
        /// </summary>
        [SettingsUISection(Locking)]
        public bool NoAbandonment
        {
            get => _disableAbandonment;

            set
            {
                _disableAbandonment = value;

                // Update system, if it's ready.
                if (HistoricalLevellingSystem.Instance is HistoricalLevellingSystem historicalLevellingSystem)
                {
                    historicalLevellingSystem.DisableAbandonment = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether building levelling should be disabled.
        /// </summary>
        [SettingsUISection(Locking)]
        public bool DisableLevelling
        {
            get => _disableLevelling;

            set
            {
                _disableLevelling = value;

                // Update system, if it's ready.
                if (HistoricalLevellingSystem.Instance is HistoricalLevellingSystem historicalLevellingSystem)
                {
                    historicalLevellingSystem.DisableLevelling = value;
                }
            }
        }

        /// <summary>
        /// Sets a value indicating whether all eligible buildings should be level-locked.
        /// </summary>
        [SettingsUIButton]
        [SettingsUISection(ApplyToExisting)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsNotInGame))]
        public bool LockAllBuildings
        {
            set
            {
                ExistingBuildingSystem.Instance?.LockAllBuildings();
            }
        }

        /// <summary>
        /// Sets a value indicating whether all eligible buildings should have level-locking cleared.
        /// </summary>
        [SettingsUIButton]
        [SettingsUISection(ApplyToExisting)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsNotInGame))]
        public bool UnlockAllBuildings
        {
            set
            {
                ExistingBuildingSystem.Instance?.UnlockAllBuildings();
            }
        }

        /// <summary>
        /// Sets a value indicating whether all eligible buildings should have abandonment cleared.
        /// </summary>
        ///
        [SettingsUIButton]
        [SettingsUISection(ApplyToExisting)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsNotInGame))]
        public bool RemoveAllAbandonment
         {
             set
            {
                ExistingBuildingSystem.Instance?.RemoveAllAbandonment();
            }
        }

        /// <summary>
        /// Restores mod settings to default.
        /// </summary>
        public override void SetDefaults()
        {
            _disableLevelling = false;
        }

        /// <summary>
        /// Determines whether we're currently in-game (in a city) or not.
        /// </summary>
        /// <returns><c>false</c> if we're currently in-game, <c>true</c> otherwise (such as in the main menu or editor).</returns>
        public bool IsNotInGame() => GameManager.instance.gameMode != GameMode.Game;
    }
}