using System.ComponentModel;

namespace TechnoartSDK.Extensions;

public static class EnumExtensions
{
    public static string ToDescriptionString(this Enum value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        if (fieldInfo != null)
        {
            var attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attributes.Length > 0)
            {
                return ((DescriptionAttribute)attributes[0]).Description;
            }
        }
        return value.ToString(); // Fallback to the enum name if no description is found
    }
}
