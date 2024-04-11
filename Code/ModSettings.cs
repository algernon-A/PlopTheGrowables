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
    [SettingsUIShowGroupName(Locking, ApplyToExisting, DisableAll)]
    [SettingsUITabOrder(Locking, ApplyToExisting, DisableAll)]
    [SettingsUIGroupOrder(Locking, ApplyToExisting, DisableAll)]
    public class ModSettings : ModSetting
    {
        // String constants for categories.
        private const string Locking = "Locking";
        private const string ApplyToExisting = "ApplyToExisting";
        private const string DisableAll = "DisableAll";

        // Backing fields.
        private bool _disableLevelling = false;
        private bool _lockPloppedBuildings = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public ModSettings(IMod mod)
            : base(mod)
        {
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

        /*
        /// <summary>
        /// Gets or sets a value indicating whether building abandonment should be disabled.
        /// </summary>
        [SettingsUISection(Locking)]
        public bool NoAbandonment { get; set; }
        */

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

                // Assign contra value to ensure that JSON contains at least one non-default value.
                Contra = value;

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

        /*
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
             }
        }
        */

        /// <summary>
        /// Gets or sets a value indicating whether, well, nothing really.
        /// This is just the inverse of <see cref="DisableLevelling"/>, to ensure the the JSON contains at least one non-default value.
        /// This is to workaround a bug where the settings file isn't overwritten when there are no non-default settings.
        /// </summary>
        [SettingsUIHidden]
        public bool Contra { get; set; } = true;

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