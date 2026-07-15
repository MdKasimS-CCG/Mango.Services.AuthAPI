namespace Mango.Services.AuthAPI.Models.Dto
{
    /// <summary>
    /// You Will Notice Projects Are Completely Independent Of 
    /// Other API Projects Or Even UI Project
    /// </summary>
    public class ResponseDto
    {
        public bool IsSuccess { get; set; } = true;
        public string Message { get; set; } = "";
        public object? Result { get; set; }
    }
}
