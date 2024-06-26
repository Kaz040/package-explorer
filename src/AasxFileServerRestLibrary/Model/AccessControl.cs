/* 
 * DotAAS Part 2 | HTTP/REST | Entire Interface Collection
 *
 * The entire interface collection as part of Details of the Asset Administration Shell Part 2
 *
 * OpenAPI spec version: Final-Draft
 * 
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SwaggerDateConverter = IO.Swagger.Client.SwaggerDateConverter;

namespace IO.Swagger.Model
{
    /// <summary>
    /// AccessControl
    /// </summary>
    [DataContract]
    public partial class AccessControl : IEquatable<AccessControl>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccessControl" /> class.
        /// </summary>
        /// <param name="accessPermissionRule">accessPermissionRule.</param>
        /// <param name="defaultEnvironmentAttributes">defaultEnvironmentAttributes.</param>
        /// <param name="defaultPermissions">defaultPermissions.</param>
        /// <param name="defaultSubjectAttributes">defaultSubjectAttributes.</param>
        /// <param name="selectableEnvironmentAttributes">selectableEnvironmentAttributes.</param>
        /// <param name="selectablePermissions">selectablePermissions.</param>
        /// <param name="selectableSubjectAttributes">selectableSubjectAttributes.</param>
        public AccessControl(List<AccessPermissionRule> accessPermissionRule = default(List<AccessPermissionRule>), Reference defaultEnvironmentAttributes = default(Reference), Reference defaultPermissions = default(Reference), Reference defaultSubjectAttributes = default(Reference), Reference selectableEnvironmentAttributes = default(Reference), Reference selectablePermissions = default(Reference), Reference selectableSubjectAttributes = default(Reference))
        {
            this.AccessPermissionRule = accessPermissionRule;
            this.DefaultEnvironmentAttributes = defaultEnvironmentAttributes;
            this.DefaultPermissions = defaultPermissions;
            this.DefaultSubjectAttributes = defaultSubjectAttributes;
            this.SelectableEnvironmentAttributes = selectableEnvironmentAttributes;
            this.SelectablePermissions = selectablePermissions;
            this.SelectableSubjectAttributes = selectableSubjectAttributes;
        }

        /// <summary>
        /// Gets or Sets AccessPermissionRule
        /// </summary>
        [DataMember(Name = "accessPermissionRule", EmitDefaultValue = false)]
        public List<AccessPermissionRule> AccessPermissionRule { get; set; }

        /// <summary>
        /// Gets or Sets DefaultEnvironmentAttributes
        /// </summary>
        [DataMember(Name = "defaultEnvironmentAttributes", EmitDefaultValue = false)]
        public Reference DefaultEnvironmentAttributes { get; set; }

        /// <summary>
        /// Gets or Sets DefaultPermissions
        /// </summary>
        [DataMember(Name = "defaultPermissions", EmitDefaultValue = false)]
        public Reference DefaultPermissions { get; set; }

        /// <summary>
        /// Gets or Sets DefaultSubjectAttributes
        /// </summary>
        [DataMember(Name = "defaultSubjectAttributes", EmitDefaultValue = false)]
        public Reference DefaultSubjectAttributes { get; set; }

        /// <summary>
        /// Gets or Sets SelectableEnvironmentAttributes
        /// </summary>
        [DataMember(Name = "selectableEnvironmentAttributes", EmitDefaultValue = false)]
        public Reference SelectableEnvironmentAttributes { get; set; }

        /// <summary>
        /// Gets or Sets SelectablePermissions
        /// </summary>
        [DataMember(Name = "selectablePermissions", EmitDefaultValue = false)]
        public Reference SelectablePermissions { get; set; }

        /// <summary>
        /// Gets or Sets SelectableSubjectAttributes
        /// </summary>
        [DataMember(Name = "selectableSubjectAttributes", EmitDefaultValue = false)]
        public Reference SelectableSubjectAttributes { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class AccessControl {\n");
            sb.Append("  AccessPermissionRule: ").Append(AccessPermissionRule).Append("\n");
            sb.Append("  DefaultEnvironmentAttributes: ").Append(DefaultEnvironmentAttributes).Append("\n");
            sb.Append("  DefaultPermissions: ").Append(DefaultPermissions).Append("\n");
            sb.Append("  DefaultSubjectAttributes: ").Append(DefaultSubjectAttributes).Append("\n");
            sb.Append("  SelectableEnvironmentAttributes: ").Append(SelectableEnvironmentAttributes).Append("\n");
            sb.Append("  SelectablePermissions: ").Append(SelectablePermissions).Append("\n");
            sb.Append("  SelectableSubjectAttributes: ").Append(SelectableSubjectAttributes).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public virtual string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="input">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object input)
        {
            return this.Equals(input as AccessControl);
        }

        /// <summary>
        /// Returns true if AccessControl instances are equal
        /// </summary>
        /// <param name="input">Instance of AccessControl to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(AccessControl input)
        {
            if (input == null)
                return false;

            return
                (
                    this.AccessPermissionRule == input.AccessPermissionRule ||
                    this.AccessPermissionRule != null &&
                    input.AccessPermissionRule != null &&
                    this.AccessPermissionRule.SequenceEqual(input.AccessPermissionRule)
                ) &&
                (
                    this.DefaultEnvironmentAttributes == input.DefaultEnvironmentAttributes ||
                    (this.DefaultEnvironmentAttributes != null &&
                    this.DefaultEnvironmentAttributes.Equals(input.DefaultEnvironmentAttributes))
                ) &&
                (
                    this.DefaultPermissions == input.DefaultPermissions ||
                    (this.DefaultPermissions != null &&
                    this.DefaultPermissions.Equals(input.DefaultPermissions))
                ) &&
                (
                    this.DefaultSubjectAttributes == input.DefaultSubjectAttributes ||
                    (this.DefaultSubjectAttributes != null &&
                    this.DefaultSubjectAttributes.Equals(input.DefaultSubjectAttributes))
                ) &&
                (
                    this.SelectableEnvironmentAttributes == input.SelectableEnvironmentAttributes ||
                    (this.SelectableEnvironmentAttributes != null &&
                    this.SelectableEnvironmentAttributes.Equals(input.SelectableEnvironmentAttributes))
                ) &&
                (
                    this.SelectablePermissions == input.SelectablePermissions ||
                    (this.SelectablePermissions != null &&
                    this.SelectablePermissions.Equals(input.SelectablePermissions))
                ) &&
                (
                    this.SelectableSubjectAttributes == input.SelectableSubjectAttributes ||
                    (this.SelectableSubjectAttributes != null &&
                    this.SelectableSubjectAttributes.Equals(input.SelectableSubjectAttributes))
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hashCode = 41;
                if (this.AccessPermissionRule != null)
                    hashCode = hashCode * 59 + this.AccessPermissionRule.GetHashCode();
                if (this.DefaultEnvironmentAttributes != null)
                    hashCode = hashCode * 59 + this.DefaultEnvironmentAttributes.GetHashCode();
                if (this.DefaultPermissions != null)
                    hashCode = hashCode * 59 + this.DefaultPermissions.GetHashCode();
                if (this.DefaultSubjectAttributes != null)
                    hashCode = hashCode * 59 + this.DefaultSubjectAttributes.GetHashCode();
                if (this.SelectableEnvironmentAttributes != null)
                    hashCode = hashCode * 59 + this.SelectableEnvironmentAttributes.GetHashCode();
                if (this.SelectablePermissions != null)
                    hashCode = hashCode * 59 + this.SelectablePermissions.GetHashCode();
                if (this.SelectableSubjectAttributes != null)
                    hashCode = hashCode * 59 + this.SelectableSubjectAttributes.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// To validate all properties of the instance
        /// </summary>
        /// <param name="validationContext">Validation context</param>
        /// <returns>Validation Result</returns>
        IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            yield break;
        }
    }
}
