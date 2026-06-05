using System;
using System.Text;
using Backend.Application.Constants;
using Backend.Application.DTOs.Provinces;
using Backend.Application.DTOs.Wards;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Backend.Share.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class ProvinceService : IProvinceService
{
    private readonly IProvinceRepository _provinceRepository;
    private readonly IWardRepository _wardRepository;
    private readonly HttpClient _httpClient;
    private readonly ISerializeService _serializeService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProvinceService(IProvinceRepository provinceRepository, IWardRepository wardRepository, IHttpClientFactory httpClientFactory, ISerializeService serializeService, IHttpContextAccessor httpContextAccessor)
    {
        _provinceRepository = provinceRepository;
        _wardRepository = wardRepository;
        _httpClient = httpClientFactory.CreateClient();
        _serializeService = serializeService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ApiResponse> CreateAsync(CreateProvinceDto obj)
    {
        var userRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds();
        var isAdmin = userRoleIds != null && userRoleIds.Any(x => x == CommonConstants.Role.ADMIN);
        var model = obj.ToEntity();
        var isExistingProvince = await _provinceRepository.AnyAsync(
                x => x.Name.ToLower() == model.Name.ToLower() &&
                     !x.IsDeleted);

        if (isExistingProvince)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        var isExistingCode = await _provinceRepository.AnyAsync(
                x => x.Code.ToLower() == model.Code.ToLower() &&
                     !x.IsDeleted);
        if (isExistingCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData
            );
        if (!isAdmin)
            return ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden), ApiCodeConstants.Common.Forbidden);
        await _provinceRepository.CreateAsync(model);
        await _provinceRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateProvinceDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _provinceRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new ProvinceDetailDto
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
                Name = x.Name,
                Code = x.Code,
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _provinceRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();
        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _provinceRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetWardsAsync(int provinceId)
    {
        var data = await _wardRepository
            .FindByCondition(x => x.ProvinceId == provinceId)
            .Select(x => new WardDetailDto
            {
                Id = x.Id,
                Name = x.Name,
                ProvinceId = x.ProvinceId
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var userRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds();
        var isAdmin = userRoleIds != null && userRoleIds.Any(x => x == CommonConstants.Role.ADMIN);
        var isDeleted = await _provinceRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();
        if (!isAdmin)
            return ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden), ApiCodeConstants.Common.Forbidden);
        await _provinceRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SyncDataAsync()
    {
        var provincesFromDb = await _provinceRepository.GetAllAsync();
        var wardFromDb = await _wardRepository.GetAllAsync();

        var baseDir = AppContext.BaseDirectory;
        var provincePath = Path.Combine(baseDir, "StaticFiles", $"data.json");
        using StreamReader provinceReader = new(provincePath);
        var provinceJson = await provinceReader.ReadToEndAsync();
        var listProvince = _serializeService.Deserialize<List<ProvinceSyncData>>(provinceJson);

        var listAddProvince = new List<Province>();
        if (listProvince != null && listProvince.Any())
        {
            foreach (var item in listProvince)
            {
                var existData = provincesFromDb.Find(x => x.Name.ToLower().Contains(item.name.ToLower()));
                if (existData != null)
                {
                    existData.Slug = StringHelper.Slugify(item.name);
                    existData.Name = item.name.Contains("Thành phố") ? item.name : $"{item.place_type} {item.name}";
                    existData.IsCentral = item.place_type.Contains("Thành phố");
                    existData.Code = item.province_code;
                    existData.Type = item.place_type.Contains("Thành phố") ? "city" : "province";
                }
                else
                {
                    var addProvince = new Province
                    {
                        Code = item.province_code,
                        Name = item.name.Contains("Thành phố") ? item.name : $"{item.place_type} {item.name}",
                        IsCentral = item.place_type.Contains("Thành phố"),
                        Slug = StringHelper.Slugify(item.name),
                        Type = item.place_type.Contains("Thành phố") ? "city" : "province"
                    };
                    listAddProvince.Add(addProvince);
                }
            }
            await _provinceRepository.UpdateListAsync(provincesFromDb);
            if (listAddProvince.Any())
            {
                await _provinceRepository.CreateListAsync(listAddProvince);
            }
            await _provinceRepository.SaveChangesAsync();

            var listWard = listProvince
                .SelectMany(x => x.wards)
                .ToList();
            var listWardAdd = new List<Ward>();
            if (listWard != null && listWard.Any())
            {
                foreach (var item in listWard)
                {
                    if (provincesFromDb.Any())
                        listAddProvince.AddRange(provincesFromDb);

                    var provinceId = listAddProvince
                        .Find(x => x.Code == item.province_code)?.Id ?? 0;
                    var existData = wardFromDb.Find(x => x.Name.Normalize(NormalizationForm.FormC) == item.name.Normalize(NormalizationForm.FormC));
                    if (existData != null)
                    {
                        existData.Code = item.ward_code;
                        existData.Name = item.name;
                        existData.ProvinceCode = item.province_code;
                        existData.Slug = StringHelper.Slugify(item.name);
                        existData.Type = item.name.Contains("Phường") ? "ward" : "commune";
                        existData.ProvinceId = provinceId;
                    }
                    else
                    {
                        var addWard = new Ward
                        {
                            Code = item.ward_code,
                            Name = item.name,
                            Slug = StringHelper.Slugify(item.name),
                            Type = item.name.Contains("Phường") ? "ward" : "commune",
                            ProvinceCode = item.province_code,
                            ProvinceId = provinceId,
                        };
                        listWardAdd.Add(addWard);
                    }
                }
            }

            await _wardRepository.UpdateListAsync(wardFromDb);
            if (listWardAdd.Any())
            {
                await _wardRepository.CreateListAsync(listWardAdd);
            }
            await _wardRepository.SaveChangesAsync();
        }

        return ApiResponse.Success(provincesFromDb);

    }

    public async Task<ApiResponse> UpdateAsync(UpdateProvinceDto obj)
    {
        var userRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds();
        var isAdmin = userRoleIds != null && userRoleIds.Any(x => x == CommonConstants.Role.ADMIN);
        var existData = await _provinceRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();
        var isDuplicatedName = await _provinceRepository.AnyAsync(
                 x => x.Name.ToLower() == obj.Name.ToLower() &&
                      !x.IsDeleted && x.Id != obj.Id);

        if (isDuplicatedName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        var isDuplicatedCode = await _provinceRepository.AnyAsync(
                 x => x.Code.ToLower() == obj.Code.ToLower() &&
                      !x.IsDeleted && x.Id != obj.Id);
        if (isDuplicatedCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData
            );

        obj.ToEntity(existData);
        if (!isAdmin)
            return ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden), ApiCodeConstants.Common.Forbidden);
        await _provinceRepository.UpdateAsync(existData);

        var listWard = await _wardRepository
            .FindByConditionAsync(x => x.ProvinceId == obj.Id);
        listWard.ForEach(item => item.ProvinceCode = obj.Code);

        await _wardRepository.UpdateListAsync(listWard);

        await _provinceRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateProvinceDto> objs)
    {
        throw new NotImplementedException();
    }
}
