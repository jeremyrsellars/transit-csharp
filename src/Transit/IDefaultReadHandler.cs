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
    /// Processes a non-decodable transit value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IDefaultReadHandler<out T>
    {
        /// <summary>
        /// Reads a transit representation that cannot otherwise be read.
        /// </summary>
        /// <param name="tag">The transit value's tag.</param>
        /// <param name="representation">The transit value's representation.</param>
        /// <returns>The resulting generic object.</returns>
        T FromRepresentation(string tag, object representation);
    }

    /// <summary>
    /// Processes a non-decodable transit value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [System.Runtime.InteropServices.Guid("02361BE4-D886-4129-89F2-46167CA32363")]
    public interface IDefaultReadHandler
    {
        /// <summary>
        /// Reads a transit representation that cannot otherwise be read.
        /// </summary>
        /// <param name="tag">The transit value's tag.</param>
        /// <param name="representation">The transit value's representation.</param>
        /// <returns>The resulting generic object.</returns>
        object FromRepresentation(string tag, object representation);
    }
}