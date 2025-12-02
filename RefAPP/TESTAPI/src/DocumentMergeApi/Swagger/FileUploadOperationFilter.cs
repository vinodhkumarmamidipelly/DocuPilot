using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace DocumentMergeApi.Swagger;

public sealed class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var formParameters = context.MethodInfo.GetParameters()
            .Where(p => p.GetCustomAttributes(typeof(FromFormAttribute), false).Any())
            .ToList();

        if (!formParameters.Any()) return;

        var formParam = formParameters.First();
        var paramType = formParam.ParameterType;
        
        // Get properties from the DTO class or individual parameters
        var properties = new Dictionary<string, OpenApiSchema>();
        var required = new HashSet<string>();

        if (paramType.IsClass && paramType != typeof(string))
        {
            // It's a DTO class - get properties from it
            var dtoProperties = paramType.GetProperties();
            foreach (var prop in dtoProperties)
            {
                if (prop.PropertyType == typeof(IFormFile) || prop.PropertyType == typeof(IFormFile[]))
                {
                    properties[prop.Name] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    };
                }
                else
                {
                    properties[prop.Name] = new OpenApiSchema
                    {
                        Type = "string"
                    };
                }

                // Check if property is nullable
                var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType);
                var isNullable = underlyingType != null || 
                    (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)) ||
                    (prop.PropertyType.IsClass && prop.PropertyType != typeof(string) && prop.PropertyType != typeof(IFormFile));
                
                if (!isNullable)
                {
                    required.Add(prop.Name);
                }
            }
        }
        else
        {
            // Individual parameters
            foreach (var param in formParameters)
            {
                if (param.ParameterType == typeof(IFormFile) || param.ParameterType == typeof(IFormFile[]))
                {
                    properties[param.Name!] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    };
                }
                else
                {
                    properties[param.Name!] = new OpenApiSchema
                    {
                        Type = "string"
                    };
                }

                if (!IsNullable(param))
                {
                    required.Add(param.Name!);
                }
            }
        }

        // Clear existing parameters
        if (operation.Parameters != null)
        {
            var paramsToRemove = operation.Parameters
                .Where(p => formParameters.Any(fp => fp.Name == p.Name))
                .ToList();
            
            foreach (var param in paramsToRemove)
            {
                operation.Parameters.Remove(param);
            }
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = properties,
                        Required = required
                    }
                }
            }
        };
    }

    private static bool IsNullable(System.Reflection.ParameterInfo parameter)
    {
        return parameter.IsOptional || 
               (parameter.ParameterType.IsGenericType && 
                parameter.ParameterType.GetGenericTypeDefinition() == typeof(Nullable<>));
    }
}

