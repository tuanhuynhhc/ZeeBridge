﻿using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using Zeebe.Client.Api.Commands;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ZeeBridge.Extenstion;

public static class WorkerJobExtension
{
    private static IJobClient? _jobClient;

    internal static void SetJobClient(this IJob job, IJobClient jobClient)
    {
        _jobClient = jobClient;
    }

    public static T GetVariables<T>(this IJob job)
    {
        return job.Variables.ParseObject<T>() ??
            throw new InvalidOperationException($"Failed to deserialize variables for job {job.Key}");
    }

    public static Dictionary<string, object> GetVariables(this IJob job)
    {
        return job.Variables.ParseObject<Dictionary<string, object>>() ??
            throw new InvalidOperationException($"Failed to deserialize variables for job {job.Key}");
    }

    public static T GetHeadersInVariables<T>(this IJob job)
    {
        Dictionary<string, object> variables = job.GetVariables();

        return variables["headers"].ToString()!.ParseObject<T>() ??
            throw new InvalidOperationException($"Failed to headers value in variables for job {job.Key}");
    }

    private static dynamic GetHeadersInVariables(this IJob job)
    {
        Dictionary<string, object> variables = job.GetVariables();
        return JObject.Parse(variables["headers"].ToString()!)  
            ?? throw new InvalidOperationException($"Failed to headers value in variables for job {job.Key}");
    }

    public static T? GetHeaderValueInVariables<T>(this IJob job, string fieldName)
    {
        var variables = job.GetHeadersInVariables();
        var fieldValue = variables[fieldName];
        if (fieldValue is null)
        {
            return default(T);
        }
        
        return (T) Convert.ChangeType(fieldValue, typeof(T));
    }

    public static T GetDataValueInVariables<T>(this IJob job, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentNullException($"{nameof(fieldName)} cannot be NULL or EMPTY");
        }

        Dictionary<string, object> variables = job.GetVariables();
        return variables[fieldName].ToString()!.ParseObject<T>() ??
            throw new InvalidOperationException($"Failed to Data Value value in variables for job {job.Key}");
    }

    public static bool MarkAsCompleted(this IJob job)
    {
        CreateCompleteJobCommand(job)
            .Send()
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        return true;
    }

    public static bool MarkAsCompleted(this IJob job, object transferData)
    {
        var data = transferData.ToJson();
        CreateCompleteJobCommand(job, data)
            .Send()
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        return true;
    }


    public static async Task MarkAsCompletedAsync(this IJob job)
    {
        await CreateCompleteJobCommand(job)
            .Send();
    }

    public static async Task MarkAsCompletedAsync(this IJob job, object transferData)
    {
        var data = transferData.ToJson();
        await CreateCompleteJobCommand(job, data)
            .Send();
    }

    public static void MarkAsFailed(this IJob job, string errorMessage)
    {
        CreateFailedJobCommand(job, errorMessage)
            .Send()
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    public static async Task MarkAsFailedAsync(this IJob job, string errorMessage)
    {
        await CreateFailedJobCommand(job, errorMessage)
            .Send();
    }

    private static IFailJobCommandStep2 CreateFailedJobCommand(IJob job, string errorMessage)
    {
        return _jobClient!.NewFailCommand(job.Key)
            .Retries(job.Retries - 1)
            .ErrorMessage(errorMessage);
    }

    private static ICompleteJobCommandStep1 CreateCompleteJobCommand(IJob job, string? data = null)
    {
        var command = _jobClient!.NewCompleteJobCommand(job.Key);
        if (string.IsNullOrWhiteSpace(data) == false)
        {
            command.Variables(data);
        }

        return command;
    }
}