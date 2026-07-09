#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

#include <chrono>
#include <thread>

namespace winrt::NoiraPlayer::Native::implementation
{
    class RenderLoopWaiter
    {
    public:
        RenderLoopWaiter() noexcept = default;

        RenderLoopWaiter(RenderLoopWaiter const&) = delete;
        RenderLoopWaiter& operator=(RenderLoopWaiter const&) = delete;

        ~RenderLoopWaiter() noexcept
        {
            if (m_timer)
            {
                CloseHandle(m_timer);
            }
        }

        bool WaitFor(std::chrono::steady_clock::duration delay) noexcept
        {
            if (delay <= std::chrono::steady_clock::duration::zero())
            {
                return true;
            }

            if (WaitWithTimer(delay))
            {
                return true;
            }

            std::this_thread::sleep_for(delay);
            return false;
        }

    private:
        bool EnsureTimer() noexcept
        {
            if (m_timer)
            {
                return true;
            }

            auto const access = static_cast<DWORD>(SYNCHRONIZE | TIMER_MODIFY_STATE);
            m_timer = CreateWaitableTimerExW(
                nullptr,
                nullptr,
                CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
                access);
            if (!m_timer)
            {
                m_timer = CreateWaitableTimerExW(nullptr, nullptr, 0, access);
            }

            return m_timer != nullptr;
        }

        bool WaitWithTimer(std::chrono::steady_clock::duration delay) noexcept
        {
            if (!EnsureTimer())
            {
                return false;
            }

            auto hundredNanoseconds = std::chrono::duration_cast<std::chrono::nanoseconds>(delay).count() / 100;
            if (hundredNanoseconds <= 0)
            {
                hundredNanoseconds = 1;
            }

            LARGE_INTEGER dueTime{};
            dueTime.QuadPart = -hundredNanoseconds;
            if (!SetWaitableTimer(m_timer, &dueTime, 0, nullptr, nullptr, FALSE))
            {
                return false;
            }

            return WaitForSingleObject(m_timer, INFINITE) == WAIT_OBJECT_0;
        }

        HANDLE m_timer{};
    };
}
