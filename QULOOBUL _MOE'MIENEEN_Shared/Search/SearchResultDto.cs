namespace QULOOBUL__MOE_MIENEEN_Shared.Search
{
    /// <summary>One global-search hit. Kind is the entity type name notifications already use
    /// (ProjectTask, ResourceRequest, Project, Event, Meeting, File), so clients route it the same way.</summary>
    public record SearchResultDto(string Kind, int Id, string Title, string? Detail);
}
