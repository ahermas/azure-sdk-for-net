// <auto-generated>
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.
//
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Microsoft.Azure.Management.ProviderHub.Models
{
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public partial class ResourceTypeExtension
    {
        /// <summary>
        /// Initializes a new instance of the ResourceTypeExtension class.
        /// </summary>
        public ResourceTypeExtension()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ResourceTypeExtension class.
        /// </summary>
        public ResourceTypeExtension(string endpointUri = default(string), IList<string> extensionCategories = default(IList<string>), System.TimeSpan? timeout = default(System.TimeSpan?))
        {
            EndpointUri = endpointUri;
            ExtensionCategories = extensionCategories;
            Timeout = timeout;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "endpointUri")]
        public string EndpointUri { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "extensionCategories")]
        public IList<string> ExtensionCategories { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "timeout")]
        public System.TimeSpan? Timeout { get; set; }

    }
}
