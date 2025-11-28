using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 10: NAFiller + StrictValidator
    /// Purpose: Safely handle missing or invalid fields
    /// Prevents: Template rendering failures, invalid documents, missing mandatory business data
    /// </summary>
    public class NAFiller
    {
        /// <summary>
        /// Fills missing optional fields with "N/A"
        /// </summary>
        public Dictionary<string, object> FillMissingOptional(
            Dictionary<string, object> mappings,
            HashSet<string> requiredFields,
            HashSet<string> optionalFields)
        {
            var result = new Dictionary<string, object>(mappings);

            foreach (var field in optionalFields)
            {
                if (!result.ContainsKey(field) || result[field] == null || 
                    (result[field] is string str && string.IsNullOrWhiteSpace(str)))
                {
                    result[field] = "N/A";
                }
            }

            return result;
        }

        /// <summary>
        /// Validates required fields and types
        /// </summary>
        public ValidationResult Validate(
            Dictionary<string, object> mappings,
            HashSet<string> requiredFields,
            Dictionary<string, FieldType>? typeConstraints = null)
        {
            var result = new ValidationResult();
            typeConstraints ??= new Dictionary<string, FieldType>();

            // Check required fields
            foreach (var field in requiredFields)
            {
                if (!mappings.ContainsKey(field) || 
                    mappings[field] == null ||
                    (mappings[field] is string str && string.IsNullOrWhiteSpace(str)))
                {
                    result.MissingRequired.Add(field);
                }
            }

            // Validate types
            foreach (var kvp in typeConstraints)
            {
                var fieldName = kvp.Key;
                var expectedType = kvp.Value;

                if (!mappings.ContainsKey(fieldName))
                    continue;

                var value = mappings[fieldName];
                if (value == null)
                    continue;

                if (!IsValidType(value, expectedType))
                {
                    result.TypeErrors.Add(new TypeError
                    {
                        Field = fieldName,
                        ExpectedType = expectedType.ToString(),
                        ActualValue = value.ToString() ?? "null"
                    });
                }
            }

            result.IsValid = result.MissingRequired.Count == 0 && result.TypeErrors.Count == 0;
            return result;
        }

        private bool IsValidType(object value, FieldType expectedType)
        {
            return expectedType switch
            {
                FieldType.Date => IsValidDate(value),
                FieldType.Number => IsValidNumber(value),
                FieldType.Email => IsValidEmail(value),
                FieldType.Url => IsValidUrl(value),
                FieldType.Text => value is string,
                FieldType.List => value is System.Collections.IEnumerable && !(value is string),
                _ => true
            };
        }

        private bool IsValidDate(object value)
        {
            if (value is DateTime)
                return true;
            if (value is string str)
            {
                return DateTime.TryParse(str, out _) ||
                       Regex.IsMatch(str, @"\d{1,2}[/-]\d{1,2}[/-]\d{2,4}") ||
                       Regex.IsMatch(str, @"\d{4}-\d{2}-\d{2}");
            }
            return false;
        }

        private bool IsValidNumber(object value)
        {
            if (value is int || value is long || value is double || value is decimal)
                return true;
            if (value is string str)
                return double.TryParse(str, out _);
            return false;
        }

        private bool IsValidEmail(object value)
        {
            if (value is string str)
            {
                return Regex.IsMatch(str, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
            }
            return false;
        }

        private bool IsValidUrl(object value)
        {
            if (value is string str)
            {
                return Uri.TryCreate(str, UriKind.Absolute, out _);
            }
            return false;
        }

        public enum FieldType
        {
            Text,
            Date,
            Number,
            Email,
            Url,
            List
        }

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> MissingRequired { get; set; } = new();
            public List<TypeError> TypeErrors { get; set; } = new();
        }

        public class TypeError
        {
            public string Field { get; set; } = string.Empty;
            public string ExpectedType { get; set; } = string.Empty;
            public string ActualValue { get; set; } = string.Empty;
        }
    }
}

