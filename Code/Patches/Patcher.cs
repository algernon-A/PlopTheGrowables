// <copyright file="Patcher.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using System;
    using System.IO;
    using System.Reflection;
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using HarmonyLib;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// A basic Harmony patching class.
    /// </summary>
    public class Patcher
    {
        private readonly string _harmonyID;
        private bool _lvoIsPatched = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Patcher"/> class.
        /// Doing so applies all annotated patches.
        /// </summary>
        /// <param name="harmonyID">Harmony ID to use.</param>
        /// <param name="log">Log to use for performing patching.</param>
        public Patcher(string harmonyID, ILog log)
        {
            // Set log reference.
            Log = log;

            // Dispose of any existing instance.
            if (Instance != null)
            {
                log.Error("existing Patcher instance detected with ID " + Instance._harmonyID + "; reverting");
                Instance.UnPatchAll();
            }

            // Set instance reference.
            Instance = this;
            _harmonyID = harmonyID;

            // Apply annotated patches.
            PatchAnnotations();
        }

        /// <summary>
        /// Gets the active instance.
        /// </summary>
        public static Patcher Instance { get; private set; }

        /// <summary>
        /// Gets a value indicating whether patches were successfully applied.
        /// </summary>
        public bool PatchesApplied { get; private set; } = false;

        /// <summary>
        /// Gets the logger to use when patching.
        /// </summary>
        public ILog Log { get; private set; }

        /// <summary>
        /// Reverts all applied patches.
        /// </summary>
        public void UnPatchAll()
        {
            if (!string.IsNullOrEmpty(_harmonyID))
            {
                Log.Info("reverting all applied patches for " + _harmonyID);
                Harmony harmonyInstance = new (_harmonyID);

                try
                {
                    harmonyInstance.UnpatchAll("_harmonyID");

                    // Clear applied flag.
                    PatchesApplied = false;
                    _lvoIsPatched = false;
                }
                catch (Exception e)
                {
                    Log.Critical(e, "exception reverting all applied patches for " + _harmonyID);
                }
            }
        }

        /// <summary>
        /// Applies patches to the <c>Land Value Overhaul</c> mod to work around its disabling of the game's <c>PropertyRenterSystem</c>.
        /// </summary>
        /// <param name="world">Active world instance.</param>
        internal void PatchLandValueOverhaul(World world)
        {
            // Don't do anything if already patched.
            if (!_lvoIsPatched)
            {
                // Check for Land Value Overhaul assembly.
                ExecutableAsset modAsset = AssetDatabase.global.GetAsset(SearchFilter<ExecutableAsset>.ByCondition(x => x.name.Equals("LandValueOverhaul")));
                if (modAsset is null)
                {
                    Log.Info("Land Value Overhaul not detected");
                    return;
                }

                if (modAsset.assembly is null)
                {
                    Log.Info("Land Value Overhaul assembly not loaded");
                    return;
                }

                // Get patch methods.
                Log.Info("Land Value Overhaul found");
                MethodInfo lvoTranspiler = AccessTools.Method(typeof(LandValueOverhaulPatches), nameof(LandValueOverhaulPatches.OnUpdateTranspiler));
                Type lvoPropertyRenterSystemType = modAsset.assembly.GetType("LandValueOverhaul.Systems.PropertyRenterSystem");
                MethodInfo targetMethod = AccessTools.Method(lvoPropertyRenterSystemType, "OnUpdate");
                if (targetMethod is null)
                {
                    Log.Error("Unable to find Land Value Overhaul PropertyRenterSystem update");
                    return;
                }

                try
                {
                    // Apply patches.
                    Harmony harmonyInstance = new (_harmonyID);
                    harmonyInstance.Patch(targetMethod, transpiler: new HarmonyMethod(lvoTranspiler));

                    // Update level queues.
                    Log.Info("Redirecting level queues to LVO");
                    FieldInfo lvoLevelUpQueue = AccessTools.Field(lvoPropertyRenterSystemType, "m_LevelupQueue");
                    FieldInfo lvoLevelDownQueue = AccessTools.Field(lvoPropertyRenterSystemType, "m_LeveldownQueue");

                    // Null checks.
                    if (lvoLevelUpQueue is null)
                    {
                        Log.Error("Unable to reflect LVO up queue");
                        return;
                    }

                    if (lvoLevelDownQueue is null)
                    {
                        Log.Error("Unable to reflect LVO down queue");
                        return;
                    }

                    // Get LVO PropertyRenterSystem instance.
                    ComponentSystemBase lvoPropertyRenterSystem = world.GetExistingSystemManaged(lvoPropertyRenterSystemType);
                    if (lvoPropertyRenterSystem is null)
                    {
                        Log.Error("Unable to get LVO PropertyRenterSystem instance");
                        return;
                    }

                    // Update queues.
                    HistoricalLevellingSystem.Instance?.SetLevelQueues((NativeQueue<Entity>)lvoLevelUpQueue.GetValue(lvoPropertyRenterSystem), (NativeQueue<Entity>)lvoLevelDownQueue.GetValue(lvoPropertyRenterSystem));

                    // Set status.
                    Log.Info("Land Value Overhaul patches successfully applied");
                    _lvoIsPatched = true;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Exception patching Land Value Overhaul");
                }
            }
        }

        /// <summary>
        /// Constructor.
        /// Applies Harmony patches.
        /// </summary>
        private void PatchAnnotations()
        {
            Log.Info("applying annotated Harmony patches for " + _harmonyID);
            Harmony harmonyInstance = new (_harmonyID);

            try
            {
                harmonyInstance.PatchAll();
                Log.Info("patching complete");

                // Set applied flag.
                PatchesApplied = true;
            }
            catch (Exception e)
            {
                Log.Critical(e, "exception applying annotated Harmony patches; reverting");
                harmonyInstance.UnpatchAll(_harmonyID);
            }
        }
    }
}
