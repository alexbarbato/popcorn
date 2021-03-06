﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace Skyward.Popcorn
{
    using ContextType = System.Collections.Generic.Dictionary<string, object>;

    /// <summary>
    /// A fluent-api style configuration object for the ApiExpander
    /// </summary>
    public class PopcornConfiguration
    {
        Expander _expander;
        public PopcornConfiguration(Expander expander) { _expander = expander; }

        public ContextType Context { get; private set; }
        public Func<object, object, object> Inspector { get; private set; }

        public bool ApplyToAllEndpoints { get; private set; } = true;

        /// <summary>
        /// Designate the context for this target
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public PopcornConfiguration SetContext(ContextType context)
        {
            if (Context != null)
                throw new InvalidOperationException("Context has already been assigned");

            Context = context;
            return this;
        }

        /// <summary>
        /// Designate an inspector to run on expanded objects
        /// </summary>
        /// <param name="inspector"></param>
        /// <returns></returns>
        public PopcornConfiguration SetInspector(Func<object, object, object> inspector)
        {
            if (Inspector != null)
                throw new InvalidOperationException("Inspector has already been assigned");
            Inspector = inspector;
            return this;
        }

        /// <summary>
        /// Set this configuration to only expand endpoints that have the ExpandResult attribute set
        /// </summary>
        /// <returns></returns>
        public PopcornConfiguration SetOptIn()
        {
            ApplyToAllEndpoints = false;
            return this;
        }

        /// <summary>
        /// Add a mapping of a data type to a projection type
        /// </summary>
        /// <typeparam name="TSourceType"></typeparam>
        /// <typeparam name="TDestType"></typeparam>
        /// <param name="defaultIncludes"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public PopcornConfiguration Map<TSourceType, TDestType>(
            string defaultIncludes = null, 
            Action<MappingDefinitionConfiguration<TSourceType, TDestType>> config = null)
        {
            var sourceType = typeof(TSourceType);
            var destType = typeof(TDestType);

            // Validate and construct the actual default includes from both attributes and those passed in at the time of the mapping
            var destTypeInfo = typeof(TDestType).GetTypeInfo();
            var parsedDefaultIncludes = (defaultIncludes == null) ? new List<PropertyReference> { } : (List<PropertyReference>)PropertyReference.Parse(defaultIncludes);
            defaultIncludes = PropertyReference.CompareAndConstructDefaultIncludes(parsedDefaultIncludes, destTypeInfo);

            // Create the configuration starting with the 'default' mapping
            var mappingConfiguration = new MappingDefinitionConfiguration<TSourceType, TDestType>
            {
                InternalMappingDefinition = new MappingDefinition
                {
                    DefaultDestinationType = destType,
                }
            };

            // And assign it
            mappingConfiguration.InternalProjectionDefinition = new ProjectionDefinition
            {
                DefaultIncludes = defaultIncludes,
                DestinationType = destType,
            };
            mappingConfiguration.InternalMappingDefinition.Destinations.Add(destType, mappingConfiguration.InternalProjectionDefinition);

            // We will allow a client to reference the same mapping multiple times to add more translations etc,
            // but ONLY if the types remain consistent!
            if (_expander.Mappings.ContainsKey(sourceType))
            {
                throw new InvalidOperationException($"Expander was default-mapped multiple times for {sourceType}.");
            }
            else
            {
                _expander.Mappings.Add(typeof(TSourceType), mappingConfiguration.InternalMappingDefinition);
            }

            if (config != null)
                config(mappingConfiguration);

            return this;
        }

        /// <summary>
        /// Attach a function that accepts the source object (if there is one), the context, and the target
        /// object itself.  It returns true (passed) or false (fail).  If any fails are encountered, then
        /// the object is not returned.
        /// </summary>
        /// <typeparam name="TSourceType"></typeparam>
        /// <param name="authorizer"></param>
        /// <returns></returns>
        public PopcornConfiguration Authorize<TSourceType>(Func<object, ContextType, TSourceType, bool> authorizer)
        {
            var sourceType = typeof(TSourceType);
            if (!_expander.Mappings.ContainsKey(sourceType))
                throw new InvalidOperationException($"Can only authorize a type that has a mapping previously specified: {sourceType}");

            _expander.Mappings[sourceType].Authorizers.Add((s,c,v) => authorizer(s,c,(TSourceType)v));

            return this;
        }

        /// <summary>
        /// Assign a factory function to create a specific type
        /// </summary>
        /// <typeparam name="TSourceType"></typeparam>
        /// <param name="factory"></param>
        /// <returns></returns>
        public PopcornConfiguration AssignFactory<TSourceType>(Func<TSourceType> factory)
        {
            if (factory != null)
            {
                _expander.Factories.Add(typeof(TSourceType), (context) => factory());
            }
            else if(_expander.Factories.ContainsKey(typeof(TSourceType)))
            {
                _expander.Factories.Remove(typeof(TSourceType));
            }
            return this;
        }

        /// <summary>
        /// Assign a factory function to create a specific type from a context object
        /// </summary>
        /// <typeparam name="TSourceType"></typeparam>
        /// <param name="factory"></param>
        /// <returns></returns>
        public PopcornConfiguration AssignFactory<TSourceType>(Func<ContextType, TSourceType> factory)
        {
            if (factory != null)
            {
                _expander.Factories.Add(typeof(TSourceType), (context) => factory(context));
            }
            else if (_expander.Factories.ContainsKey(typeof(TSourceType)))
            {
                _expander.Factories.Remove(typeof(TSourceType));
            }
            return this;
        }

        public PopcornConfiguration BlacklistExpansion<TSourceType>()
        {
            _expander.BlacklistExpansion.Add(typeof(TSourceType));
            return this;
        }

        public PopcornConfiguration BlacklistExpansion(Type type)
        {
            _expander.BlacklistExpansion.Add(type);
            return this;
        }

        public PopcornConfiguration EnableBlindExpansion(bool v)
        {
            _expander.ExpandBlindObjects = v;
            return this;
        }
    }
}