using System;
using Backend.Application.DTOs.Feedbacks;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class FeedbackMapping
{
    public static Feedback ToEntity(this CreateFeedbackDto obj)
    {
        return new Feedback
        {
            Content = obj.Content,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now,
            Title = obj.Title,
        };
    }

    public static Feedback ToEntity(this UpdateFeedbackDto obj, Feedback existData)
    {
        existData.Title = obj.Title;
        existData.Content = obj.Content;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static FeedbackDetailDto ToDto(this Feedback obj)
    {
        return new FeedbackDetailDto
        {
            Title = obj.Title,
            Content = obj.Content,
            CreatedDate = obj.CreatedDate,
            Id = obj.Id,
        };
    }
}
