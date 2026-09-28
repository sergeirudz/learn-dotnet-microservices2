using Articles.Abstractions.Enums;
using Blocks.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Submission.Domain.Entities;

namespace Submission.Persistence.Repositories;

// public class AssetTypeDefinitionRepository(SubmissionDbContext dbContext, IMemoryCache cache)
//     : CachedRepository<SubmissionDbContext, AssetTypeDefinition, AssetType>(dbContext, cache);