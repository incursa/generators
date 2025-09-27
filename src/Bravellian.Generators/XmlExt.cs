// Copyright (c) Samuel McAravey
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Bravellian.Generators;

internal static class XmlExt
{
    public static IReadOnlyDictionary<string, string> GetAttributeDict(this XElement xml) =>
        xml.Attributes().ToDictionary(a => a.Name.ToString(), a => a.Value.ToString(), StringComparer.Ordinal);

    public static TValue? TryGetValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, TKey key)
        where TValue : class
        => dict.TryGetValue(key, out TValue value) ? value : null;
}
