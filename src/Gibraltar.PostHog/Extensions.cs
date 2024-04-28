using System;
using System.Collections.Generic;

namespace Gibraltar.PostHog
{
    /// <summary>
    /// Scenario-specific extensions for the PostHog API
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Publish group information to PostHog
        /// </summary>
        /// <param name="api">The PostHog API</param>
        /// <param name="groupType">The group type (you should have very few - or even just one - of these)</param>
        /// <param name="groupKey">Your database key for the group (unique and constant over all time)</param>
        /// <param name="details">Properties for the group.</param>
        /// <remarks>Add a property called "name" to the details to specify a friendly name for the group.</remarks>
        public static void Group(this PostHogApi api, string userKey, string groupType, string groupKey, Dictionary<string, object> details = null)
        {
            var properties = new Dictionary<string, object>();

            if (details != null)
            {
                properties["$group_set"] = details;
            }

            properties["$group_type"] = groupType;
            properties["$group_key"] = groupKey;

            api.Capture("$identify", userKey, properties);
        }

        /// <summary>
        /// Identify a user to PostHog
        /// </summary>
        /// <param name="api">The PostHog API</param>
        /// <param name="userKey">Your database key for the user (unique and constant over all time)</param>
        /// <param name="properties">Optional.  A set of name-value pairs to record about the user that overwrite previous values.</param>
        /// <param name="oneTimeProperties">Optional.  A set of name-value pairs to record about the user that are only used if PostHog hasn't seen the user before.</param>
        /// <param name="groups">Optional. Group information for the user.</param>
        public static void Identify(this PostHogApi api, string userKey, Dictionary<string, object>? properties = null, 
            Dictionary<string, object>? oneTimeProperties = null, Dictionary<string, object>? groups = null)
        {
            var compositeProperties = new Dictionary<string, object>();

            if (properties != null)
            {
                compositeProperties.Add("$set", properties);
            }

            if (oneTimeProperties != null)
            {
                compositeProperties.Add("$set_once", oneTimeProperties);
            }

            if (groups?.TryGetValue("$groups", out var groupValue) == true)
            {
                compositeProperties["$groups"] = groupValue;
            }

            api.Capture("$identify", userKey, compositeProperties);
        }
    }
}
