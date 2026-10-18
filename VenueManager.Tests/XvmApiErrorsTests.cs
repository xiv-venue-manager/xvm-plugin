using VenueManager;
using Xunit;

public class XvmApiErrorsTests
{
    [Fact]
    public void A_detail_sentence_is_shown_as_is()
    {
        Assert.Equal("House Cocktail is out of stock.", XvmApiErrors.Describe(409, "{\"detail\":\"House Cocktail is out of stock.\"}"));
    }

    [Fact]
    public void A_rejected_body_shows_the_first_message()
    {
        var body = "{\"detail\":[{\"loc\":[\"body\",\"amount\"],\"msg\":\"Input should be greater than 0\",\"type\":\"greater_than\"}]}";
        Assert.Equal("Input should be greater than 0", XvmApiErrors.Describe(422, body));
    }

    [Fact]
    public void A_rejected_link_says_to_link_again()
    {
        Assert.Contains("Link", XvmApiErrors.Describe(401, "{\"detail\":\"Invalid credential\"}"));
    }

    [Fact]
    public void A_manager_only_refusal_is_said_plainly()
    {
        Assert.Equal("You need to be a manager at this venue to do that.", XvmApiErrors.Describe(403, "{\"detail\":\"Insufficient role for this action.\"}"));
    }

    [Fact]
    public void A_member_only_refusal_keeps_its_own_sentence()
    {
        Assert.Equal("This action requires a venue member.", XvmApiErrors.Describe(403, "{\"detail\":\"This action requires a venue member.\"}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    public void An_unreadable_body_falls_back_to_the_status(string body)
    {
        Assert.Equal("The server answered 500.", XvmApiErrors.Describe(500, body));
    }
}
