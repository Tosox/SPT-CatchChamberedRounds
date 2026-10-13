using EFT;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal static class PlayerExtensions
    {
        private static readonly Dictionary<Type, FieldInfo> ObservedAiFields = new Dictionary<Type, FieldInfo>();

        internal static bool IsAi(this IPlayer player)
        {
            if (player == null)
                return false;

            if (player.IsAI)
                return true;

            var type = player.GetType();
            if (!ObservedAiFields.TryGetValue(type, out var field))
            {
                field = type.GetField("IsObservedAI", BindingFlags.Public | BindingFlags.Instance);
                ObservedAiFields[type] = field;
            }

            return field != null && (bool)field.GetValue(player);
        }
    }
}
