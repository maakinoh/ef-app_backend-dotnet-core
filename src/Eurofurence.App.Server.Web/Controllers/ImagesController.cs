﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eurofurence.App.Domain.Model.Images;
using Eurofurence.App.Server.Services.Abstractions.Images;
using Eurofurence.App.Server.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.IO;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace Eurofurence.App.Server.Web.Controllers
{
    [Route("Api/[controller]")]
    public class ImagesController : BaseController
    {
        private readonly IImageService _imageService;
        private readonly IMapper _mapper;

        public ImagesController(IImageService imageService, IMapper mapper)
        {
            _imageService = imageService;
            _mapper = mapper;
        }

        /// <summary>
        ///     Retrieves a list of all images.
        /// </summary>
        /// <returns>All images.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(IEnumerable<ImageResponse>), 200)]
        public IEnumerable<ImageResponse> GetImagesAsync()
        {
            return _mapper.Map<IEnumerable<ImageResponse>>(_imageService.FindAll());
        }

        /// <summary>
        ///     Retrieves a list of all images with related IDs.
        /// </summary>
        /// <returns>All images with related IDs.</returns>
        [HttpGet("with-relations")]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(IEnumerable<ImageWithRelationsResponse>), 200)]
        public IEnumerable<ImageWithRelationsResponse> GetImagesWithRelationsAsync()
        {
            return _mapper.Map<IEnumerable<ImageWithRelationsResponse>>(
                _imageService.FindAll()
                    .Include(i => i.KnowledgeEntries)
                    .Include(i => i.FursuitBadges)
                    .Include(i => i.TableRegistrations)
                    .Include(i => i.Maps)
                    .Include(i => i.DealerArtPreviews)
                    .Include(i => i.DealerArtistThumbnails)
                    .Include(i => i.DealerArtists)
                    .Include(i => i.EventBanners)
                    .Include(i => i.EventPosters)
                    .Include(i => i.Announcements));
        }

        /// <summary>
        ///     Retrieve a single image.
        /// </summary>
        /// <param name="id">id of the requested entity</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(ImageResponse), 200)]
        public async Task<ImageResponse> GetImageAsync([FromRoute] Guid id)
        {
            return _mapper.Map<ImageResponse>(await _imageService.FindOneAsync(id).Transient404(HttpContext));
        }

        /// <summary>
        ///     Retrieve a single image content.
        /// </summary>
        /// <param name="id">id of the requested entity</param>
        [HttpGet("{id}/Content")]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(byte[]), 200)]
        public async Task<ActionResult> GetImageContentAsync([FromRoute] Guid id)
        {
            var record = await _imageService.FindOneAsync(id);
            if (record == null) return NotFound();

            var content = await _imageService.GetImageContentByImageIdAsync(id);
            return File(content, record.MimeType);
        }
        
        /// <summary>
        ///     Retrieve a single image content using hash code (preferred, as it allows caching).
        /// </summary>
        /// <param name="id">id of the requested entity</param>
        /// <param name="contentHashBase64Encoded">Base64 Encoded ContentHashSha1 of the requested entity</param>
        [HttpGet("{id}/Content/with-hash:{contentHashBase64Encoded}")]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(byte[]), 200)]
        [ResponseCache(Duration = 60 * 60 * 24, Location = ResponseCacheLocation.Any)]
        public async Task<ActionResult> GetImageWithHashContentAsync(
            [EnsureNotNull][FromRoute] Guid id,
            [EnsureNotNull][FromRoute] string contentHashBase64Encoded)
        {
            var record = await _imageService.FindOneAsync(id);
            if (record == null) return NotFound();

            var contentHash =
                Encoding.Default.GetString(Convert.FromBase64String(contentHashBase64Encoded));

            if (record.ContentHashSha1 != contentHash)
            {
                return Redirect($"./with-hash:{Convert.ToBase64String(Encoding.Default.GetBytes(record.ContentHashSha1))}");
            }

            var content = await _imageService.GetImageContentByImageIdAsync(id);
            return File(content, record.MimeType);
        }

        [Authorize(Roles = "System,Developer,KnowledgeBase-Maintainer")]
        [HttpPut("{id}")]
        public async Task<ActionResult> PutImageAsync([FromRoute] Guid id, IFormFile file)
        {
            var record = await _imageService.FindOneAsync(id);
            if (record == null) return NotFound();

            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                var result = await _imageService.ReplaceImageAsync(record.Id, file.FileName, ms);
                return Ok(_mapper.Map<ImageResponse>(result));
            }
        }

        [Authorize(Roles = "System,Developer,KnowledgeBase-Maintainer")]
        [HttpPost]
        public async Task<ActionResult> PostImageAsync(IFormFile file)
        {
            if (file == null)
            {
                return BadRequest();
            }

            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                var result = await _imageService.InsertImageAsync(file.FileName, ms);
                return Ok(_mapper.Map<ImageResponse>(result));
            }
        }

        /// <summary>
        ///     Delete an image.
        /// </summary>
        /// <param name="id"></param>
        [Authorize(Roles = "System,Developer,KnowledgeBase-Maintainer")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(string), 404)]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteImageAsync(
            [EnsureNotNull][FromRoute] Guid id
        )
        {
            var existingRecord = await _imageService.FindOneAsync(id);
            if (existingRecord == null) return NotFound($"No record found with id {id}");

            await _imageService.DeleteOneAsync(id);

            return NoContent();
        }
    }
}