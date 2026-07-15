using Mango.MessageBus;
using Mango.Services.AuthAPI.Models.Dto;
using Mango.Services.AuthAPI.RabbitMqSender;
using Mango.Services.AuthAPI.Service;
using Mango.Services.AuthAPI.Service.IService;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
        protected IConfiguration _configuration;

        // TODO: I have added respective interface & its concrete class in Mango.Integration. Switch to use that one.
        // Since we are not going to keep any cloud based MQ as primary queuing technology.
        protected IMessageProducer _messageBus;

        //TODO: With Clean Architecture Try To Decouple This
        /// <summary> 
        /// SDE-Observation
        /// You are coupling DbContext with Controllers. This will also couple 
        /// EFCore with application. Changing ORM, will break solution.
        /// Violating Separation Of Concerns principles at architect level
        /// </summary>
        /// <param name="db"></param>
        /// <param name="mapper"></param>
        public AuthAPIController(IAuthService service, IMessageProducer messageBus, IConfiguration configuration)
        {
            _authService = service;
            _response = new();
            _messageBus = messageBus;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistrationRequestDto model)
        {
            //TODO: When the user was created, timestamp is missing
            var errorMessage = await _authService.Register(model);

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

            await _messageBus.SendMessage(userDto, _configuration.GetValue<string>("TopicAndQueueNames:RegisterUserQueue"));
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
