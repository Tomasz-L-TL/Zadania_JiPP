using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.HttpResults;
using Week02.Api.Contracts;
using Week02.Api.Domain;
using Week02.Api.Repositories;

namespace Week02.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        RouteGroupBuilder v1 = app.MapGroup("/api/v1/products").WithTags("Products v1");
        v1.MapGet("", ListV1).WithName("ListProductsV1");
        v1.MapGet("/{id:int}", GetV1).WithName("GetProductV1");

        RouteGroupBuilder v2 = app.MapGroup("/api/v2/products").WithTags("Products v2");
        v2.MapGet("", ListV2).WithName("ListProductsV2");
        v2.MapGet("/{id:int}", GetV2).WithName("GetProductV2");
    }

    private static Ok<IReadOnlyList<ProductV1Dto>> ListV1(IProductCatalog catalog)
    {
        IReadOnlyList<ProductV1Dto> items = catalog.GetAll().Select(ToV1).ToArray();
        return TypedResults.Ok(items);
    }

    private static Ok<IReadOnlyList<ProductV2Dto>> ListV2(IProductCatalog catalog)
    {
        IReadOnlyList<ProductV2Dto> items = catalog.GetAll().Select(ToV2).ToArray();
        return TypedResults.Ok(items);
    }

    private static Results<Ok<ProductV1Dto>, BadRequest<ApiError>, NotFound<ApiError>> GetV1(
        int id, IProductCatalog catalog)
    {
        if (id <= 0)
        {
            return TypedResults.BadRequest(new ApiError("invalid_id", "Identyfikator musi byc wiekszy od zera."));
        }

        Product? product = catalog.GetById(id);
        if (product is null)
        {
            return TypedResults.NotFound(new ApiError("product_not_found", $"Produkt o ID {id} nie istnieje."));
        }

        return TypedResults.Ok(ToV1(product));
    }

    private static Results<Ok<ProductV2Dto>, BadRequest<ApiError>, NotFound<ApiError>> GetV2(
        int id, IProductCatalog catalog)
    {
        if (id <= 0)
        {
            return TypedResults.BadRequest(new ApiError("invalid_id", "Identyfikator musi byc wiekszy od zera."));
        }

        Product? product = catalog.GetById(id);
        if (product is null)
        {
            return TypedResults.NotFound(new ApiError("product_not_found", $"Produkt o ID {id} nie istnieje."));
        }

        return TypedResults.Ok(ToV2(product));
    }

    private static ProductV1Dto ToV1(Product product) =>
        new(product.Id, product.Name, product.Price, product.InStock);

    private static ProductV2Dto ToV2(Product product) =>
        new(product.Id, product.Name,
            new MoneyV2Dto(product.Price, product.Currency), product.Category, product.InStock);
}
