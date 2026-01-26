using System.ComponentModel;
using Microsoft.SemanticKernel;
using WebApi.Models;
using WebApi.Repositories;

namespace WebApi.Services.Chat.SkPlugins;

public class AppointmentPlugin(AppointmentRepository appointmentRepository, Guid userId)
{
    [KernelFunction]
    [Description("Get all appointments for the current user. By default, only returns active appointments (not completed or cancelled).")]
    public async Task<List<Appointment>> GetUserAppointmentsAsync(
        [Description("Whether to include completed and cancelled appointments")] bool includeCompleted = false)
    {
        return await appointmentRepository.GetAppointmentsAsync(userId, includeCompleted);
    }

    [KernelFunction]
    [Description("Book a new appointment for the current user. Returns the created appointment.")]
    public async Task<Appointment> BookAppointmentAsync(
        [Description("The name of the clinic or healthcare provider")] string? clinicName,
        [Description("The preferred date and time for the appointment")] DateTime? dateTime,
        [Description("The reason for the appointment")] string? reason,
        [Description("The urgency level: Emergency, High, Medium, or Low")] string? urgency = null)
    {
        return await appointmentRepository.BookAsync(userId, clinicName, dateTime, reason, urgency);
    }

    [KernelFunction]
    [Description("Cancel an appointment by ID. Returns true if the appointment was found and cancelled, false otherwise.")]
    public async Task<bool> CancelAppointmentAsync(
        [Description("The appointment ID to cancel")] Guid appointmentId)
    {
        return await appointmentRepository.CancelAsync(appointmentId);
    }
}
