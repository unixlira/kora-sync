using KazakoraAgent.Core.Queue;
using Xunit;

namespace KazakoraAgent.Core.Tests;

public class RetryPolicyTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(4, 120)]
    [InlineData(5, 300)]
    public void delay_for_next_attempt_follows_the_specified_schedule(int attemptCount, int expectedSeconds)
    {
        var policy = new RetryPolicy();

        var delay = policy.DelayForNextAttempt(attemptCount);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    public void should_retry_up_to_5_attempts(int attemptCount, bool expected)
    {
        var policy = new RetryPolicy();

        Assert.Equal(expected, policy.ShouldRetry(attemptCount));
    }

    [Fact]
    public void max_attempts_is_5()
    {
        Assert.Equal(5, new RetryPolicy().MaxAttempts);
    }
}
