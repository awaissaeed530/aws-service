using aws_service.BackgroundServices;
using aws_service.Database;
using aws_service.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add Services to IOC
builder.Services.AddTransient<ISSLService, SSLService>();
builder.Services.AddTransient<IInstanceService, InstanceService>();
builder.Services.AddTransient<IHostedZoneService, HostedZoneService>();
builder.Services.AddTransient<ILoadBalancerService, LoadBalancerService>();
builder.Services.AddTransient<IDomainRecordService, DomainRecordService>();
builder.Services.AddTransient<IOperationCrudService, OperationCrudService>();
builder.Services.AddTransient<IDomainRegistrationService, DomainRegistrationService>();
builder.Services.AddTransient<IDomainAvailabilityService, DomainAvailabilityService>();
builder.Services.AddTransient<IEC2Service, EC2Service>();

// Add Controllers Endpoints
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Add Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Required by EFCore
builder.Services.AddHttpContextAccessor();

// Register ApplicationDbContext with InMemory Database
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase("db"));

// Register Background Hosted Services
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    var jobKey = new JobKey("Operation");
    q.AddJob<OperationService>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("operation-trigger")
                    .WithCronSchedule("0/5 * * ? * * *"));
});
builder.Services.AddQuartzHostedService();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
