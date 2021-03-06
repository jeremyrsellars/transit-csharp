// Copyright � 2014 Rick Beerendonk. All Rights Reserved.
//
// This code is a C# port of the Java version created and maintained by Cognitect, therefore
//
// Copyright � 2014 Cognitect. All Rights Reserved.
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
    /// Interface for reading values in transit format.
    /// </summary>
    [System.Runtime.InteropServices.Guid("9DBDDBF6-B0A3-440C-8E3A-3B7A6D11A6A2")]
    public interface IReader
    {
        /// <summary>
        /// Reads a single value from an input source.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <returns>The value.</returns>
        T Read<T>();
        
        object Read();
    }
}