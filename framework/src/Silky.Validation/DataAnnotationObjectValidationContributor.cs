using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Silky.Core.DependencyInjection;
using Silky.Core.Extensions.Collections.Generic;
using Silky.Core.Utils;
using Microsoft.Extensions.Options;


namespace Silky.Validation
{
    public class DataAnnotationObjectValidationContributor : IObjectValidationContributor, ITransientDependency
    {
        public const int MaxRecursiveParameterValidationDepth = 8;

        protected IServiceProvider ServiceProvider { get; }
        protected SilkyValidationOptions Options { get; }

        public DataAnnotationObjectValidationContributor(
            IOptions<SilkyValidationOptions> options,
            IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Options = options.Value;
        }

        public void AddErrors(ObjectValidationContext context)
        {
            ValidateObjectRecursively(context.Errors, context.ValidatingObject, currentDepth: 1);
        }

        protected virtual void ValidateObjectRecursively(List<ValidationResult> errors, object validatingObject,
            int currentDepth)
        {
            if (currentDepth > MaxRecursiveParameterValidationDepth)
            {
                return;
            }

            if (validatingObject == null)
            {
                return;
            }

            AddErrors(errors, validatingObject);

            //Validate items of enumerable
            if (validatingObject is IEnumerable enumerable)
            {
                if (!(enumerable is IQueryable))
                {
                    foreach (var item in enumerable)
                    {
                        //Do not recursively validate for primitive objects
                        if (item == null || TypeHelper.IsPrimitiveExtended(item.GetType()))
                        {
                            break;
                        }

                        ValidateObjectRecursively(errors, item, currentDepth + 1);
                    }
                }

                return;
            }

            var validatingObjectType = validatingObject.GetType();

            //Do not recursively validate for primitive objects
            if (TypeHelper.IsPrimitiveExtended(validatingObjectType))
            {
                return;
            }

            if (Options.IgnoredTypes.Any(t => t.IsInstanceOfType(validatingObject)))
            {
                return;
            }

            var properties = TypeDescriptor.GetProperties(validatingObject).Cast<PropertyDescriptor>();
            foreach (var property in properties)
            {
                if (property.Attributes.OfType<DisableValidationAttribute>().Any())
                {
                    continue;
                }

                ValidateObjectRecursively(errors, property.GetValue(validatingObject), currentDepth + 1);
            }
        }

        public void AddErrors(List<ValidationResult> errors, object validatingObject)
        {
            var properties = TypeDescriptor.GetProperties(validatingObject).Cast<PropertyDescriptor>();

            foreach (var property in properties)
            {
                AddPropertyErrors(validatingObject, property, errors);
            }

            if (validatingObject is IValidatableObject validatableObject)
            {
                errors.AddRange(
                    validatableObject.Validate(new ValidationContext(validatableObject, ServiceProvider, null))
                );
            }
        }

        protected virtual void AddPropertyErrors(object validatingObject, PropertyDescriptor property,
            List<ValidationResult> errors)
        {
            var validationAttributes = property.Attributes.OfType<ValidationAttribute>().ToArray();
            if (validationAttributes.IsNullOrEmpty())
            {
                return;
            }

            var validationContext = new ValidationContext(validatingObject, ServiceProvider, null)
            {
                DisplayName = property.DisplayName,
                MemberName = property.Name
            };

            foreach (var attribute in validationAttributes)
            {
                var result = attribute.GetValidationResult(property.GetValue(validatingObject), validationContext);
                if (result != null)
                {
                    errors.Add(result);
                }
            }
        }
    }
}