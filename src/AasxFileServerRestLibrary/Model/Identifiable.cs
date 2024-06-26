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
    /// Identifiable
    /// </summary>
    [DataContract]
    public partial class Identifiable : Referable, IEquatable<Identifiable>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Identifiable" /> class.
        /// </summary>
        /// <param name="administration">administration.</param>
        /// <param name="identification">identification (required).</param>
        public Identifiable(AdministrativeInformation administration = default(AdministrativeInformation), string identification = default(string), string category = default(string), List<LangString> description = default(List<LangString>), List<LangString> displayName = default(List<LangString>), string idShort = default(string), ModelType modelType = default(ModelType)) : base(category, description, displayName, idShort, modelType)
        {
            // to ensure "identification" is required (not null)
            if (identification == null)
            {
                throw new InvalidDataException("identification is a required property for Identifiable and cannot be null");
            }
            else
            {
                this.Identification = identification;
            }
            this.Administration = administration;
        }

        /// <summary>
        /// Gets or Sets Administration
        /// </summary>
        [DataMember(Name = "administration", EmitDefaultValue = false)]
        public AdministrativeInformation Administration { get; set; }

        /// <summary>
        /// Gets or Sets Identification
        /// </summary>
        [DataMember(Name = "identification", EmitDefaultValue = false)]
        public string Identification { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class Identifiable {\n");
            sb.Append("  ").Append(base.ToString().Replace("\n", "\n  ")).Append("\n");
            sb.Append("  Administration: ").Append(Administration).Append("\n");
            sb.Append("  Identification: ").Append(Identification).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public override string ToJson()
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
            return this.Equals(input as Identifiable);
        }

        /// <summary>
        /// Returns true if Identifiable instances are equal
        /// </summary>
        /// <param name="input">Instance of Identifiable to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(Identifiable input)
        {
            if (input == null)
                return false;

            return base.Equals(input) &&
                (
                    this.Administration == input.Administration ||
                    (this.Administration != null &&
                    this.Administration.Equals(input.Administration))
                ) && base.Equals(input) &&
                (
                    this.Identification == input.Identification ||
                    (this.Identification != null &&
                    this.Identification.Equals(input.Identification))
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
                int hashCode = base.GetHashCode();
                if (this.Administration != null)
                    hashCode = hashCode * 59 + this.Administration.GetHashCode();
                if (this.Identification != null)
                    hashCode = hashCode * 59 + this.Identification.GetHashCode();
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
