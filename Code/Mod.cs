// <copyright file="Mod.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using System.Reflection;
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Game;
    using Game.Buildings;
    using Game.Modding;
    using Game.Simulation;

    /// <summary>
    /// The base mod class for instantiation by the game.
    /// </summary>
    public sealed class Mod : IMod
    {
        /// <summary>
        /// The mod's default name.
        /// </summary>
        public const string ModName = "Plop the Growables";

        /// <summary>
        /// Gets the active instance reference.
        /// </summary>
        public static Mod Instance { get; private set; }

        /// <summary>
        /// Gets the mod's active log.
        /// </summary>
        internal ILog Log { get; private set; }

        /// <summary>
        /// Gets the mod's active settings configuration.
        /// </summary>
        internal ModSettings ActiveSettings { get; private set; }

        /// <summary>
        /// Called by the game when the mod is loaded.
        /// </summary>
        /// <param name="updateSystem">Game update system.</param>
        public void OnLoad(UpdateSystem updateSystem)
        {
            // Set instance reference.
            Instance = this;

            // Initialize logger.
            Log = LogManager.GetLogger(ModName);
#if DEBUG
            Log.Info("setting logging level to Debug");
            Log.effectivenessLevel = Level.Debug;
#endif
            Log.Info($"loading {ModName} version {Assembly.GetExecutingAssembly().GetName().Version}");

            // Apply harmony patches.
            new Patcher("algernon-PlopTheGrowables", Log);

            // Activate UI system.
            updateSystem.UpdateAt<PlopTheGrowablesUISystem>(SystemUpdatePhase.UIUpdate);

            // Register mod settings to game options UI.
            ActiveSettings = new (this);
            ActiveSettings.RegisterInOptionsUI();

            // Load translations.
            Localization.LoadTranslations(ActiveSettings, Log);

            // Load saved settings.
            AssetDatabase.global.LoadSettings("PlopTheGrowables", ActiveSettings, new ModSettings(this));

            // Disable game zone check system.
            updateSystem.World.GetOrCreateSystemManaged<ZoneCheckSystem>().Enabled = false;

            // Activate custom levelling system.
            updateSystem.UpdateAfter<HistoricalLevellingSystem, PropertyRenterSystem>(SystemUpdatePhase.GameSimulation);

            // Activate tagging systems.
            updateSystem.UpdateAfter<ExistingBuildingSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<SpawnedBuildingSystem, BuildingConstructionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<PloppedBuildingSystem>(SystemUpdatePhase.ModificationEnd);

            // Activate custom zone check system; must run after we've assigned ploppable flags.
            updateSystem.UpdateAfter<SelectiveZoneCheckSystem, PloppedBuildingSystem>(SystemUpdatePhase.ModificationEnd);
        }

        /// <summary>
        /// Called by the game when the mod is disposed of.
        /// </summary>
        public void OnDispose()
        {
            Log.Info("disposing");
            Instance = null;

            // Clear settings menu entry.
            if (ActiveSettings != null)
            {
                ActiveSettings.UnregisterInOptionsUI();
                ActiveSettings = null;
            }

            // Revert harmony patches.
            Patcher.Instance?.UnPatchAll();
        }
    }
}
