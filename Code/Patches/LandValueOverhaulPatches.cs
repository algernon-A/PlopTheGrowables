// <copyright file="LandValueOverhaulPatches.cs" company="algernon (K. Algernon A. Sheppard)">
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
    using HarmonyLib;

    /// <summary>
    /// Harmony patches for the <c>Land Value Overhaul</c> mod to implement building level locking.
    /// </summary>
    internal class LandValueOverhaulPatches
    {
        /// <summary>
        /// Harmony transpiler for <c>PropertyRenterSystem.OnUpdate</c> to override game levelling.
        /// </summary>
        /// <param name="instructions">Original ILCode.</param>
        /// <param name="original">Method being patched.</param>
        /// <returns>Modified ILCode.</returns>
        internal static IEnumerable<CodeInstruction> OnUpdateTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            Patcher.Instance.Log.Info($"Transpiling {original.DeclaringType}:{original.Name}");

            // Levelup and Leveldown job types and local indices.
            int levelUpJobIndex = int.MaxValue;
            int levelDownJobIndex = int.MaxValue;
            Type levelUpJobType = Type.GetType("LandValueOverhaul.Systems.PropertyRenterSystem+LevelupJob,LandValueOverhaul", true);
            Type levelDownJobType = Type.GetType("LandValueOverhaul.Systems.PropertyRenterSystem+LeveldownJob,LandValueOverhaul", true);

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

                        // Skip forward until stloc.s 4, indicating the end of the job creation block.
                        while (!(instruction.opcode == OpCodes.Stloc_S && instruction.operand is LocalBuilder localBuilder2 && localBuilder2.LocalIndex == 4))
                        {
                            instructionEnumerator.MoveNext();
                            instruction = instructionEnumerator.Current;
                        }

                        // Skip current instruction (the stloc.s 4);
                        Mod.Instance.Log.Debug($"resuming after {instruction.opcode} {instruction.operand}");
                        continue;
                    }
                }

                yield return instruction;
            }
        }
    }
}
