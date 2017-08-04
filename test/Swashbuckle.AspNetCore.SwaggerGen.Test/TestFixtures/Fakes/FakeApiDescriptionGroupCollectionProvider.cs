using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Buffers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.DataAnnotations.Internal;
using Moq;
using Newtonsoft.Json;
using Microsoft.Extensions.ObjectPool;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.DataAnnotations;

namespace Swashbuckle.AspNetCore.SwaggerGen.Test
{
    public class FakeApiDescriptionGroupCollectionProvider : IApiDescriptionGroupCollectionProvider
    {
        private readonly List<ControllerActionDescriptor> _actionDescriptors;

        public FakeApiDescriptionGroupCollectionProvider()
        {
            _actionDescriptors = new List<ControllerActionDescriptor>();
        }

        public FakeApiDescriptionGroupCollectionProvider Add(
            string httpMethod,
            string routeTemplate,
            string actionFixtureName,
            string controllerFixtureName = "NotAnnotated"
        )
        {
            _actionDescriptors.Add(
                CreateActionDescriptor(httpMethod, routeTemplate, actionFixtureName, controllerFixtureName));
            return this;
        }

        public ApiDescriptionGroupCollection ApiDescriptionGroups
        {
            get
            {
                var apiDescriptions = GetApiDescriptions();
                var group = new ApiDescriptionGroup("default", apiDescriptions);
                return new ApiDescriptionGroupCollection(new[] { group }, 1);
            }
        }

        private ControllerActionDescriptor CreateActionDescriptor(
            string httpMethod,
            string routeTemplate,
            string actionFixtureName,
            string controllerFixtureName
        )
        {
            var descriptor = new ControllerActionDescriptor();
            descriptor.SetProperty(new ApiDescriptionActionData());

            descriptor.ActionConstraints = new List<IActionConstraintMetadata>();
            if (httpMethod != null)
                descriptor.ActionConstraints.Add(new HttpMethodActionConstraint(new[] { httpMethod }));

            descriptor.AttributeRouteInfo = new AttributeRouteInfo { Template = routeTemplate };

            descriptor.MethodInfo = typeof(FakeActions).GetMethod(actionFixtureName);
            if (descriptor.MethodInfo == null)
                throw new InvalidOperationException(
                    string.Format("{0} is not declared in ActionFixtures", actionFixtureName));

            descriptor.Parameters = descriptor.MethodInfo.GetParameters()
                .Select(paramInfo => new ParameterDescriptor
                    {
                        Name = paramInfo.Name,
                        ParameterType = paramInfo.ParameterType,
                        BindingInfo = BindingInfo.GetBindingInfo(paramInfo.GetCustomAttributes(false))
                    })
                .ToList();

            var controllerType = typeof(FakeControllers).GetNestedType(controllerFixtureName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            if (controllerType == null)
                throw new InvalidOperationException(
                    string.Format("{0} is not declared in ControllerFixtures", controllerFixtureName));
            descriptor.ControllerTypeInfo = controllerType.GetTypeInfo();

            descriptor.FilterDescriptors = descriptor.MethodInfo.GetCustomAttributes<ProducesResponseTypeAttribute>()
                .Select((filter) => new FilterDescriptor(filter, FilterScope.Action))
                .ToList();

            return descriptor;
        }

        private IReadOnlyList<ApiDescription> GetApiDescriptions()
        {
            var context = new ApiDescriptionProviderContext(_actionDescriptors);

            var options = new MvcOptions();
            options.InputFormatters.Add(new JsonInputFormatter(Mock.Of<ILogger>(), new JsonSerializerSettings(), ArrayPool<char>.Shared, new DefaultObjectPoolProvider()));
            options.OutputFormatters.Add(new JsonOutputFormatter(new JsonSerializerSettings(), ArrayPool<char>.Shared));

            var optionsAccessor = new Mock<IOptions<MvcOptions>>();
            optionsAccessor.Setup(o => o.Value).Returns(options);

            var constraintResolver = new Mock<IInlineConstraintResolver>();
            constraintResolver.Setup(i => i.ResolveConstraint("int")).Returns(new IntRouteConstraint());

            var provider = new DefaultApiDescriptionProvider(
                optionsAccessor.Object,
                constraintResolver.Object,
                CreateDefaultProvider()
            );

            provider.OnProvidersExecuting(context);
            provider.OnProvidersExecuted(context);
            return new ReadOnlyCollection<ApiDescription>(context.Results);
        }

        public IModelMetadataProvider CreateDefaultProvider()
        {
            var detailsProviders = new IMetadataDetailsProvider[]
            {
                new DefaultBindingMetadataProvider(),//CreateMessageProvider()),
                new DefaultValidationMetadataProvider(),
                new DataAnnotationsMetadataProvider(Options.Create<MvcDataAnnotationsLocalizationOptions>(new MvcDataAnnotationsLocalizationOptions()),null)
            };

            var compositeDetailsProvider = new DefaultCompositeMetadataDetailsProvider(detailsProviders);
            return new DefaultModelMetadataProvider(compositeDetailsProvider);
        }

        private static ModelBindingMessageProvider CreateMessageProvider()
        {
            return new MyModelBindingMessageProvider();
        }
    }

    public class MyModelBindingMessageProvider : ModelBindingMessageProvider
    {
        private Func<object, string> _ValueMustNotBeNullAccessor;
        private Func<object, object, string> _AttemptedValueIsInvalidAccessor;
        private Func<object, string> _UnknownValueIsInvalidAccessor;
        private Func<object, string> _MissingBindRequiredValueAccessor;
        private Func<string> _MissingKeyOrValueAccessor;
        private Func<object, string> _ValueIsInvalidAccessor;
        private Func<object, string> _ValueMustBeANumberAccessor;

        public MyModelBindingMessageProvider()
        {
            _MissingBindRequiredValueAccessor = name => $"A value for the '{ name }' property was not provided.";
            _MissingKeyOrValueAccessor = () => $"A value is required.";
            _ValueMustNotBeNullAccessor = value => $"The value '{ value }' is invalid.";
            _AttemptedValueIsInvalidAccessor = (value, name) => $"The value '{ value }' is not valid for { name }.";
            _UnknownValueIsInvalidAccessor = name => $"The supplied value is invalid for { name }.";
            _ValueIsInvalidAccessor = value => $"The value '{ value }' is invalid.";
            _ValueMustBeANumberAccessor = name => $"The field { name } must be a number.";
        }

        public override Func<string, string, string> AttemptedValueIsInvalidAccessor => _AttemptedValueIsInvalidAccessor;
        public override Func<string, string> MissingBindRequiredValueAccessor => _MissingBindRequiredValueAccessor;
        public override Func<string> MissingKeyOrValueAccessor => _MissingKeyOrValueAccessor;
   //     public override Func<string> MissingRequestBodyRequiredValueAccessor => _MissingRequestBodyRequiredValueAccessor;
        public override Func<string, string> UnknownValueIsInvalidAccessor => _UnknownValueIsInvalidAccessor;
        public override Func<string, string> ValueIsInvalidAccessor => _ValueIsInvalidAccessor;
        public override Func<string, string> ValueMustBeANumberAccessor => _ValueMustBeANumberAccessor;
        public override Func<string, string> ValueMustNotBeNullAccessor => _ValueMustNotBeNullAccessor;
       
    }
}