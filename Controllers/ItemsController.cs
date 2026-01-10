using Microsoft.AspNetCore.Mvc;
using SkyFlipperSolo.Services;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly ItemDetailsService _itemDetailsService;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(ItemDetailsService itemDetailsService, ILogger<ItemsController> logger)
    {
        _itemDetailsService = itemDetailsService;
        _logger = logger;
    }

    /// <summary>
    /// Search for items by name or alternative name.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<ItemDetails>>> SearchItems([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query parameter is required");

        try
        {
            var results = await _itemDetailsService.SearchItems(query);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for items with query: {Query}", query);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get item details by tag.
    /// </summary>
    [HttpGet("{tag}")]
    public async Task<ActionResult<ItemDetails>> GetItemByTag(string tag)
    {
        try
        {
            var results = await _itemDetailsService.SearchItems(tag);
            var item = results.FirstOrDefault(i => i.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
            
            if (item == null)
                return NotFound($"Item with tag '{tag}' not found");

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting item by tag: {Tag}", tag);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Add an alternative name for an item.
    /// </summary>
    [HttpPost("{tag}/alternative-names")]
    public async Task<ActionResult> AddAlternativeName(string tag, [FromBody] AddAlternativeNameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Alternative name is required");

        try
        {
            await _itemDetailsService.AddAlternativeName(tag, request.Name);
            return Ok(new { message = $"Alternative name '{request.Name}' added for '{tag}'" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding alternative name for tag: {Tag}", tag);
            return StatusCode(500, "Internal server error");
        }
    }
}

public record AddAlternativeNameRequest(string Name);
