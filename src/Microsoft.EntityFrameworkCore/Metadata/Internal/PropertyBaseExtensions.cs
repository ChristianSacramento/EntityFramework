// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public static class PropertyBaseExtensions
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static int GetStoreGeneratedIndex([NotNull] this IPropertyBase propertyBase)
            => propertyBase.GetPropertyIndexes().StoreGenerationIndex;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static int GetRelationshipIndex([NotNull] this IPropertyBase propertyBase)
            => propertyBase.GetPropertyIndexes().RelationshipIndex;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static int GetIndex([NotNull] this IPropertyBase property)
            => property.GetPropertyIndexes().Index;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static PropertyIndexes GetPropertyIndexes([NotNull] this IPropertyBase propertyBase)
            => propertyBase.AsPropertyBase().PropertyIndexes;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static PropertyIndexes CalculateIndexes([NotNull] this IEntityType entityType, [NotNull] IPropertyBase propertyBase)
        {
            var index = 0;
            var shadowIndex = 0;
            var originalValueIndex = 0;
            var relationshipIndex = 0;
            var storeGenerationIndex = 0;

            var baseCounts = entityType.BaseType?.GetCounts();
            if (baseCounts != null)
            {
                index = baseCounts.PropertyCount;
                shadowIndex = baseCounts.ShadowCount;
                originalValueIndex = baseCounts.OriginalValueCount;
                relationshipIndex = baseCounts.RelationshipCount;
                storeGenerationIndex = baseCounts.StoreGeneratedCount;
            }

            PropertyIndexes callingPropertyIndexes = null;

            foreach (var property in entityType.GetDeclaredProperties())
            {
                var indexes = new PropertyIndexes(
                    index: index++,
                    originalValueIndex: property.RequiresOriginalValue() ? originalValueIndex++ : -1,
                    shadowIndex: property.IsShadowProperty ? shadowIndex++ : -1,
                    relationshipIndex: property.IsKeyOrForeignKey() ? relationshipIndex++ : -1,
                    storeGenerationIndex: property.MayBeStoreGenerated() ? storeGenerationIndex++ : -1);

                TrySetIndexes(property, indexes);

                if (propertyBase == property)
                {
                    callingPropertyIndexes = indexes;
                }
            }

            var isNotifying = entityType.GetChangeTrackingStrategy() != ChangeTrackingStrategy.Snapshot;

            foreach (var navigation in entityType.GetDeclaredNavigations())
            {
                var indexes = new PropertyIndexes(
                    index: index++,
                    originalValueIndex: -1,
                    shadowIndex: -1,
                    relationshipIndex: navigation.IsCollection() && isNotifying ? -1 : relationshipIndex++,
                    storeGenerationIndex: -1);

                TrySetIndexes(navigation, indexes);

                if (propertyBase == navigation)
                {
                    callingPropertyIndexes = indexes;
                }
            }

            foreach (var derivedType in entityType.GetDirectlyDerivedTypes())
            {
                derivedType.CalculateIndexes(propertyBase);
            }

            return callingPropertyIndexes;
        }

        private static void TrySetIndexes(IPropertyBase propertyBase, PropertyIndexes indexes)
            => propertyBase.AsPropertyBase().PropertyIndexes = indexes;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static PropertyAccessors GetPropertyAccessors([NotNull] this IPropertyBase propertyBase)
            => propertyBase.AsPropertyBase().Accessors;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IClrPropertyGetter GetGetter([NotNull] this IPropertyBase propertyBase)
            => propertyBase.AsPropertyBase().Getter;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IClrPropertySetter GetSetter([NotNull] this IPropertyBase propertyBase)
            => propertyBase.AsPropertyBase().Setter;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static PropertyInfo GetPropertyInfo([NotNull] this IPropertyBase propertyBase)
            => propertyBase.AsPropertyBase().PropertyInfo;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static FieldInfo GetFieldInfo([NotNull] this IPropertyBase propertyBase)
            => propertyBase.AsPropertyBase().FieldInfo;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static MemberInfo GetMemberInfo(
            [NotNull] this IPropertyBase propertyBase,
            bool forConstruction,
            bool forSet)
        {
            var memberInfo = propertyBase.FindMemberInfo(forConstruction, forSet);

            var message = memberInfo as string;
            if (message != null)
            {
                throw new InvalidOperationException(message);
            }

            return (MemberInfo)memberInfo;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static object FindMemberInfo(
            [NotNull] this IPropertyBase propertyBase,
            bool forConstruction,
            bool forSet)
        {
            var propertyInfo = propertyBase.GetPropertyInfo();
            var fieldInfo = propertyBase.GetFieldInfo();
            var isCollectionNav = (propertyBase as INavigation)?.IsCollection() == true;

            var mode = propertyBase.GetPropertyAccessMode();
            if (mode == null
                || mode == PropertyAccessMode.FieldDuringConstruction)
            {
                if (forConstruction
                    && fieldInfo != null
                    && !fieldInfo.IsInitOnly)
                {
                    return fieldInfo;
                }

                if (forConstruction)
                {
                    if (fieldInfo != null)
                    {
                        if (!fieldInfo.IsInitOnly)
                        {
                            return fieldInfo;
                        }

                        if (mode == PropertyAccessMode.FieldDuringConstruction
                            && !isCollectionNav)
                        {
                            return CoreStrings.ReadonlyField(fieldInfo.Name, propertyBase.DeclaringEntityType.DisplayName());
                        }
                    }

                    if (mode == PropertyAccessMode.FieldDuringConstruction)
                    {
                        if (!isCollectionNav)
                        {
                            return CoreStrings.NoBackingField(
                                propertyBase.Name, propertyBase.DeclaringEntityType.DisplayName(), nameof(PropertyAccessMode));
                        }
                        return null;
                    }
                }

                if (forSet)
                {
                    var setterProperty = propertyInfo?.FindSetterProperty();
                    if (setterProperty != null)
                    {
                        return setterProperty;
                    }

                    if (fieldInfo != null)
                    {
                        if (!fieldInfo.IsInitOnly)
                        {
                            return fieldInfo;
                        }

                        if (!isCollectionNav)
                        {
                            return CoreStrings.ReadonlyField(fieldInfo.Name, propertyBase.DeclaringEntityType.DisplayName());
                        }
                    }

                    if (!isCollectionNav)
                    {
                        return CoreStrings.NoFieldOrSetter(propertyBase.Name, propertyBase.DeclaringEntityType.DisplayName());
                    }

                    return null;
                }

                var getterPropertyInfo = propertyInfo?.FindGetterProperty();
                if (getterPropertyInfo != null)
                {
                    return getterPropertyInfo;
                }

                if (fieldInfo != null)
                {
                    return fieldInfo;
                }

                return CoreStrings.NoFieldOrGetter(propertyBase.Name, propertyBase.DeclaringEntityType.DisplayName());
            }

            if (mode == PropertyAccessMode.Field)
            {
                if (fieldInfo == null)
                {
                    if (!forSet
                        || !isCollectionNav)
                    {
                        return CoreStrings.NoBackingField(
                            propertyBase.Name, propertyBase.DeclaringEntityType.DisplayName(), nameof(PropertyAccessMode));
                    }
                    return null;
                }

                if (forSet
                    && fieldInfo.IsInitOnly)
                {
                    if (!isCollectionNav)
                    {
                        return CoreStrings.ReadonlyField(fieldInfo.Name, propertyBase.DeclaringEntityType.DisplayName());
                    }
                    return null;
                }

                return fieldInfo;
            }

            if (propertyInfo == null)
            {
                return CoreStrings.NoProperty(fieldInfo.Name, propertyBase.DeclaringEntityType.DisplayName(), nameof(PropertyAccessMode));
            }

            if (forSet)
            {
                var setterProperty = propertyInfo.FindSetterProperty();
                if (setterProperty == null
                    && !isCollectionNav)
                {
                    return CoreStrings.NoSetter(propertyBase.Name, propertyBase.DeclaringEntityType.DisplayName(), nameof(PropertyAccessMode));
                }

                return setterProperty;
            }

            var getterProperty = propertyInfo.FindGetterProperty();
            if (getterProperty == null)
            {
                return CoreStrings.NoGetter(propertyBase.Name, propertyBase.DeclaringEntityType.DisplayName(), nameof(PropertyAccessMode));
            }

            return getterProperty;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static Type GetClrType([NotNull] this IPropertyBase propertyBase)
            => propertyBase.AsPropertyBase().ClrType;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static PropertyBase AsPropertyBase([NotNull] this IPropertyBase propertyBase, [NotNull] [CallerMemberName] string methodName = "")
            => propertyBase.AsConcreteMetadataType<IPropertyBase, PropertyBase>(methodName);
    }
}
