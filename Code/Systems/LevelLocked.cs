// <copyright file="LevelLocked.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace PlopTheGrowables
{
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    /// <summary>
    /// Tag component to identify level-locked buildings.
    /// </summary>
    public struct LevelLocked : IComponentData, IQueryTypeParameter, IEmptySerializable
    {
    }
}
