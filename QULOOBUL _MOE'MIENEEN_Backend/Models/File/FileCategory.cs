using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.File
{
    [Table("File_Category")]
    public class FileCategory
    {
        [Key] public int CategoryId { get; set; }
        [MaxLength(100)] public string CategoryName { get; set; } = String.Empty;
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//