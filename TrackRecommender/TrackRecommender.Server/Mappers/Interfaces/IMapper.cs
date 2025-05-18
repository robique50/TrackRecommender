namespace TrackRecommender.Server.Mappers.Interfaces
{
    public interface IMapper<TEntity, TDto>
    {
        TDto ToDto(TEntity entity);
        TEntity ToEntity(TDto dto);
    }
}
