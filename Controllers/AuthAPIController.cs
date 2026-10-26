using Mango.MessageBus;
using Mango.Services.AuthAPI.Models.Dto;
using Mango.Services.AuthAPI.Service;
using Mango.Services.AuthAPI.Service.IService;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
namespace Mango.Services.AuthAPI.Controllers
{
    /// <summary>
    /// You Will Notice Projects Are Completely Independent Of 
    /// Other API Projects Or Even UI Project
    /// </summary>
    [Route("api/auth")]
    [ApiController]
    public class AuthAPIController : ControllerBase
    {
        private readonly IAuthService _authService;
        protected ResponseDto _response;
        private readonly ILogger<AuthAPIController> _logger;
        private readonly AuthQueueOptions _authQueueOptions;
        protected IMessageProducer _messageBus;

        public AuthAPIController(
            IAuthService service,
            IMessageProducer messageBus,
            ILogger<AuthAPIController> logger,
            IOptions<AuthQueueOptions> authQueueOptions)
        {
            _authService = service;
            _response = new();
            _messageBus = messageBus;
            _logger = logger;
            _authQueueOptions = authQueueOptions.Value;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistrationRequestDto model)
        {
            //TODO: When the user was created, timestamp is missing
            var (errorMessage, userId) = await _authService.Register(model);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                _response.IsSuccess = false;
                _response.Message = errorMessage;
                return BadRequest(_response);
            }

            //TODO: Check we need here Id or not. I have excluded because of privacy concerns.
            UserDto userDto = new UserDto()
            { 
                Name = model.Name,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
            };

        var registerUserQueue = _authQueueOptions.RegisterUserQueue;

        var queueOptions = new RabbitMqQueueOptions
        {
            Durable = true,
            Arguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "email.dlx",
                ["x-dead-letter-routing-key"] = registerUserQueue
            }
        };

        var messageId = Guid.NewGuid().ToString();

        try
        {
            await _messageBus.SendMessage(
                userDto,
                registerUserQueue,
                messageId,
                queueOptions);
        }
        catch (Exception ex)
        {
            // The user has already been registered. Log the publishing failure
            // without turning the successful registration into a failed response.
            _logger.LogError(
                ex,
                "User registration succeeded for user {UserId}, but publishing to queue {QueueName} failed. MessageId: {MessageId}",
                userId,
                registerUserQueue,
                messageId);
        }

        return Ok(_response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
        {
            var loginResponse = await _authService.Login(model);

            if (loginResponse.User == null)
            {
                _response.IsSuccess = false;
                _response.Message = "Username or password is incorrect";
                return BadRequest(_response);
            }

            //For valid case loginResponse will geive UserDto & token
            _response.Result = loginResponse;
            return Ok(_response);
        }

        [HttpPost("assignrole")]
        public async Task<IActionResult> AssignRole([FromBody] RegistrationRequestDto model)
        {
            //Even phone number is mismatch, it is creating roles
            var assignRoleSucessful = await _authService.AssignRole(model.Email, model.Role.ToUpper());

            if (!assignRoleSucessful)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }

            return Ok(_response);
        }
    }
}
