﻿// Copyright © 2014 Rick Beerendonk. All Rights Reserved.
//
// This code is a C# port of the Java version created and maintained by Cognitect, therefore
//
// Copyright © 2014 Cognitect. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS-IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Sellars.Transit.Alpha
{
    /// <summary>
    /// Interface for writing values in transit format.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    public interface IWriter<T>
    {
        /// <summary>
        /// Writes a single value to an output target.
        /// </summary>
        /// <param name="value">The value to write.</param>
        void Write(T value);
    }

    [System.Runtime.InteropServices.Guid("6EE1B1D0-CD00-4C43-9069-514025D42788")]
    public interface IWriter
    {
        /// <summary>
        /// Writes a single value to an output target.
        /// </summary>
        /// <param name="value">The value to write.</param>
        void Write(object value);
    }
}
