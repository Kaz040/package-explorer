/********************************************************************************
* Copyright (c) {2019 - 2024} Contributors to the Eclipse Foundation
*
* See the NOTICE file(s) distributed with this work for additional
* information regarding copyright ownership.
*
* This program and the accompanying materials are made available under the
* terms of the Apache License Version 2.0 which is available at
* https://www.apache.org/licenses/LICENSE-2.0
*
* SPDX-License-Identifier: Apache-2.0
********************************************************************************/

/*
 * This code has been automatically generated by aas-core-codegen.
 * Do NOT edit or append.
 */

using Aas = AasCore.Aas3_0;  // renamed
using CodeAnalysis = System.Diagnostics.CodeAnalysis;

using System.Collections.Generic;  // can't alias

namespace AasCore.Aas3_0
{
    /// <summary>
    /// Provide reporting for de/serialization and verification.
    /// </summary>
    public static class Reporting
    {
        /// <summary>
        /// Capture a path segment of a value in a model.
        /// </summary>
        public abstract class Segment
        {
            // Intentionally empty.
        }

        public class NameSegment : Segment
        {
            public readonly string Name;
            public NameSegment(string name)
            {
                Name = name;
            }
        }

        public class IndexSegment : Segment
        {
            public readonly int Index;
            public IndexSegment(int index)
            {
                Index = index;
            }
        }

        private static readonly System.Text.RegularExpressions.Regex VariableNameRe = (
            new System.Text.RegularExpressions.Regex(
                @"^[a-zA-Z_][a-zA-Z_0-9]*$"));

        /// <summary>
        /// Generate a JSON Path based on the path segments.
        /// </summary>
        /// <remarks>
        /// See, for example, this page for more information on JSON path:
        /// https://support.smartbear.com/alertsite/docs/monitors/api/endpoint/jsonpath.html
        /// </remarks>
        public static string GenerateJsonPath(
            ICollection<Segment> segments)
        {
            var parts = new List<string>(segments.Count);
            int i = 0;
            foreach (var segment in segments)
            {
                string? part;
                switch (segment)
                {
                    case NameSegment nameSegment:
                        if (VariableNameRe.IsMatch(nameSegment.Name))
                        {
                            part = (i == 0) ? nameSegment.Name : $".{nameSegment.Name}";
                        }
                        else
                        {
                            string escaped = nameSegment.Name
                                .Replace("\\", "\\\\")
                                .Replace("\"", "\\\"")
                                .Replace("\b", "\\b")
                                .Replace("\f", "\\f")
                                .Replace("\n", "\\n")
                                .Replace("\r", "\\r")
                                .Replace("\t", "\\t");
                            part = $"[\"{escaped}\"]";
                        }
                        break;
                    case IndexSegment indexSegment:
                        part = $"[{indexSegment.Index}]";
                        break;
                    default:
                        throw new System.InvalidOperationException(
                            $"Unexpected segment type: {segment.GetType()}");
                }
                parts.Add(part);
                i++;
            }
            return string.Join("", parts);
        }

        /// <summary>
        /// Escape special characters for XPath.
        /// </summary>
        private static string EscapeForXPath(
            string text)
        {
            // Mind the order, as we need to replace '&' first.
            //
            // For some benchmarks, see:
            // https://stackoverflow.com/questions/1321331/replace-multiple-string-elements-in-c-sharp
            return (
                text
                    // Even though ampersand, less-then etc. can not occur in valid element names,
                    // we escape them here for easier debugging and better bug reports.
                    .Replace("&", "&amp;")
                    .Replace("/", "&#47;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;")
            );
        }

        /// <summary>
        /// Generate a relative XPath based on the path segments.
        /// </summary>
        /// <remarks>
        /// This method leaves out the leading slash ('/'). This is helpful if
        /// to embed the error report in a larger document with a prefix etc.
        /// </remarks>
        public static string GenerateRelativeXPath(
            ICollection<Segment> segments)
        {
            var parts = new List<string>(segments.Count);
            foreach (var segment in segments)
            {
                string? part;
                switch (segment)
                {
                    case NameSegment nameSegment:
                        part = EscapeForXPath(nameSegment.Name);
                        break;
                    case IndexSegment indexSegment:
                        part = $"*[{indexSegment.Index}]";
                        break;
                    default:
                        throw new System.InvalidOperationException(
                            $"Unexpected segment type: {segment.GetType()}");
                }
                parts.Add(part);
            }
            return string.Join("/", parts);
        }

        /// <summary>
        /// Represent an error during the deserialization or the verification.
        /// </summary>
        public class Error
        {
            [CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming")]
            internal readonly LinkedList<Segment> _pathSegments = new LinkedList<Segment>();
            public readonly string Cause;
            public ICollection<Segment> PathSegments => _pathSegments;
            public Error(string cause)
            {
                Cause = cause;
            }

            public void PrependSegment(Segment segment)
            {
                _pathSegments.AddFirst(segment);
            }
        }
    }  // public static class Reporting
}  // namespace AasCore.Aas3_0

/*
 * This code has been automatically generated by aas-core-codegen.
 * Do NOT edit or append.
 */
