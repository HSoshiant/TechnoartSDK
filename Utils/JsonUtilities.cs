using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TechnoartSDK.Utils;

public static class JsonUtilities
{
    public static JsonElement GetJsonSchema<T, K>(List<string>? excludedProps = null, List<string>? includedProps = null)
    {
        if ((excludedProps is null) == (includedProps is null))
        {
            throw new ArgumentException("Either excludedProps or includedProps must be provided, but not both.");
        }

        var names = (includedProps ?? excludedProps)!;

        var js = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        if (typeInfo.Type != typeof(K)) return;

                        if (includedProps is not null)
                        {
                            foreach (var prop in typeInfo.Properties.ToList())
                            {
                                if (!names.Contains(prop.Name, StringComparer.InvariantCultureIgnoreCase))
                                {
                                    typeInfo.Properties.Remove(prop);
                                }
                            }
                        }
                        else
                        {
                            foreach (var prop in typeInfo.Properties.ToList())
                            {
                                if (names.Contains(prop.Name, StringComparer.InvariantCultureIgnoreCase))
                                {
                                    typeInfo.Properties.Remove(prop);
                                }
                            }
                        }
                    }
                }
            }
        };

        return AIJsonUtilities.CreateJsonSchema(typeof(T), serializerOptions: js);
    }
}
