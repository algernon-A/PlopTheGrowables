// <copyright file="PropertyRenterSystemPatches.cs" company="algernon (K. Algernon A. Sheppard)">
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
    using Game.Simulation;
    using HarmonyLib;

    /// <summary>
    /// Harmony patches for <see cref="PropertyRenterSystem"/> to implement building level locking.
    /// </summary>
    [HarmonyPatch]
    internal class PropertyRenterSystemPatches
    {
        /// <summary>
        /// Harmony transpiler for <c>PropertyRenterSystem.OnUpdate</c> to override game levelling.
        /// </summary>
        /// <param name="instructions">Original ILCode.</param>
        /// <param name="original">Method being patched.</param>
        /// <returns>Modified ILCode.</returns>
        [HarmonyPatch(typeof(PropertyRenterSystem), "OnUpdate")]
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> UpdateDataTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            Patcher.Instance.Log.Info($"Transpiling {original.Name}");

            // Levelup and Leveldown job types and local indices.
            int levelUpJobIndex = int.MaxValue;
            int levelDownJobIndex = int.MaxValue;
            Type levelUpJobType = Type.GetType("Game.Simulation.PropertyRenterSystem+LevelupJob,Game", true);
            Type levelDownJobType = Type.GetType("Game.Simulation.PropertyRenterSystem+LeveldownJob,Game", true);

            // Get local indices.
            foreach (LocalVariableInfo localVarInfo in original.GetMethodBody().LocalVariables)
            {
                if (localVarInfo.LocalType == levelUpJobType)
                {
                    levelUpJobIndex = localVarInfo.LocalIndex;
                    Patcher.Instance.Log.Debug($"Found level up index {levelUpJobIndex}");
                }
                else if (localVarInfo.LocalType == levelDownJobType)
                {
                    levelDownJobIndex = localVarInfo.LocalIndex;
                    Patcher.Instance.Log.Debug($"Found level down index {levelDownJobIndex}");
                }
            }

            // Iterate through all instructions in original method.
            IEnumerator<CodeInstruction> instructionEnumerator = instructions.GetEnumerator();
            while (instructionEnumerator.MoveNext())
            {
                CodeInstruction instruction = instructionEnumerator.Current;

                if (instruction.operand is LocalBuilder localBuilder)
                {
                    if (localBuilder.LocalIndex == levelUpJobIndex || localBuilder.LocalIndex == levelDownJobIndex)
                    {
                        Mod.Instance.Log.Debug($"Skipping local {localBuilder.LocalIndex} from {instruction.opcode} {instruction.operand}");

                        // Skip forward until stloc.3, indicating the end of the job creation block.
                        while (instruction.opcode != OpCodes.Stloc_3)
                        {
                            instructionEnumerator.MoveNext();
                            instruction = instructionEnumerator.Current;
                        }

                        // Skip current instruction (the stloc.3);
                        Mod.Instance.Log.Debug($"resuming after {instruction.opcode} {instruction.operand}");
                        continue;
                    }
                }

                yield return instruction;
            }
        }
    }
}
