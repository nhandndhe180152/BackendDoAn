using System;
using Backend.Application.DTOs.Feedbacks;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IFeedbackService : IServiceBase<int, CreateFeedbackDto, UpdateFeedbackDto, DTParameter>
{
}
