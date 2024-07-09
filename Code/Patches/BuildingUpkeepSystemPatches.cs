// <copyright file="BuildingUpkeepSystemPatches.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using Colossal.Logging;
    using Game.Simulation;
    using HarmonyLib;
    using Unity.Entities;

    /// <summary>
    /// Harmony patches for <see cref="BuildingUpkeepSystem"/> to implement building level locking.
    /// </summary>
    [HarmonyPatch]
    internal class BuildingUpkeepSystemPatches
    {
        /// <summary>
        /// Harmony transpiler for <c>PropertyRenterSystem.OnUpdate</c> to override game levelling.
        /// </summary>
        /// <param name="instructions">Original ILCode.</param>
        /// <param name="original">Method being patched.</param>
        /// <returns>Modified ILCode.</returns>
        [HarmonyPatch(typeof(BuildingUpkeepSystem), "OnUpdate")]
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> OnUpdateTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            ILog log = Patcher.Instance.Log;
            log.Info($"Transpiling {original.DeclaringType}:{original.Name}");

            // Levelup and Leveldown job types and local indices.
            int levelUpJobIndex = int.MaxValue;
            int levelDownJobIndex = int.MaxValue;
            Type levelUpJobType = Type.GetType("Game.Simulation.BuildingUpkeepSystem+LevelupJob,Game", true);
            Type levelDownJobType = Type.GetType("Game.Simulation.BuildingUpkeepSystem+LeveldownJob,Game", true);
            MethodInfo dependencySetter = AccessTools.PropertySetter(typeof(SystemBase), "Dependency");

            if (dependencySetter is null)
            {
                log.Error("unable to reflect SystemBase.Dependency setter; aborting transpiler");
            }
            else
            {
                // Get local indices.
                foreach (LocalVariableInfo localVarInfo in original.GetMethodBody().LocalVariables)
                {
                    if (localVarInfo.LocalType == levelUpJobType)
                    {
                        levelUpJobIndex = localVarInfo.LocalIndex;
                        log.Debug($"Found level up index {levelUpJobIndex}");
                    }
                    else if (localVarInfo.LocalType == levelDownJobType)
                    {
                        levelDownJobIndex = localVarInfo.LocalIndex;
                        log.Debug($"Found level down index {levelDownJobIndex}");
                    }
                }
            }

            // Iterate through all instructions in original method.
            IEnumerator<CodeInstruction> instructionEnumerator = instructions.GetEnumerator();
            while (instructionEnumerator.MoveNext())
            {
                CodeInstruction instruction = instructionEnumerator.Current;

                if (dependencySetter is not null && instruction.operand is LocalBuilder localBuilder)
                {
                    if (localBuilder.LocalIndex == levelUpJobIndex || localBuilder.LocalIndex == levelDownJobIndex)
                    {
                        log.Debug($"Skipping local {localBuilder.LocalIndex} from {instruction.opcode} {instruction.operand}");

                        // Skip forward until we find the Dependency setter, indicating the end of the job creation block.
                        while (!instruction.Calls(dependencySetter))
                        {
                            instructionEnumerator.MoveNext();
                            instruction = instructionEnumerator.Current;
                        }

                        // Skip current instruction (the dependency setter).
                        log.Debug($"resuming after {instruction.opcode} {instruction.operand}");
                        continue;
                    }
                }

                yield return instruction;
            }
        }
    }
}
