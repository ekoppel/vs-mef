﻿namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides metadata view proxy instances for arbitrary metadata view interfaces.
    /// </summary>
    internal interface IMetadataViewProvider
    {
        /// <summary>
        /// Gets a value indicating whether this provider can create a metadata proxy for a given type.
        /// </summary>
        /// <param name="metadataType">The type of the required proxy.</param>
        /// <returns><c>true</c> if the provider can create a proxy for this type. Otherwise false.</returns>
        bool IsMetadataViewSupported(Type metadataType);

        /// <summary>
        /// Creates an instance of <typeparamref name="TMetadata"/> that acts as a strongly-typed accessor
        /// to a metadata dictionary.
        /// </summary>
        /// <typeparam name="TMetadata">The type of interface whose members are made up only of property getters.</typeparam>
        /// <param name="metadata">The metadata dictionary. This will always have a key for each property on the <typeparamref name="TMetadata"/> interface.</param>
        /// <returns>The proxy instance.</returns>
        TMetadata CreateProxy<TMetadata>(IReadOnlyDictionary<string, object> metadata);
    }
}
