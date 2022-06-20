using aws_service.BackgroundServices;
using aws_service.Database;
using aws_service.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<IDomainRegistrationService, DomainRegistrationService>();
builder.Services.AddTransient<IDomainAvailabilityService, DomainAvailabilityService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase("db"));

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
//builder.Services.AddQuartzHostedService();

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
