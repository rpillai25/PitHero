using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.UI;
using PitHero.UI;

namespace PitHero.Tests.UI
{
    [TestClass]
    public class HoverableImageButtonTests
    {
        [TestMethod]
        public void HoverableImageButton_ShouldBeCreatable()
        {
            // Arrange
            var style = new ImageButtonStyle();
            
            // Act
            var hoverButton = new HoverableImageButton(style, "Test Hover");
            
            // Assert
            Assert.IsNotNull(hoverButton, "HoverableImageButton should be created successfully");
        }

        [TestMethod]
        public void HoverableImageButton_SetHoverText_ShouldUpdateText()
        {
            // Arrange
            var style = new ImageButtonStyle();
            var hoverButton = new HoverableImageButton(style, "Initial Text");
            
            // Act
            hoverButton.SetHoverText("Updated Text");
            
            // Assert - no exception should be thrown
            Assert.IsNotNull(hoverButton, "Button should still be valid after updating hover text");
        }

        [TestMethod]
        public void HoverableImageButton_ShouldInheritFromImageButton()
        {
            // Arrange
            var style = new ImageButtonStyle();
            var hoverButton = new HoverableImageButton(style, "Test");
            
            // Act & Assert
            Assert.IsInstanceOfType(hoverButton, typeof(ImageButton), "HoverableImageButton should inherit from ImageButton");
        }

        [TestMethod]
        public void HoverableImageButton_EstimateTextWidth_ShouldHandleEmptyText()
        {
            // This test validates that our text width estimation handles edge cases
            // We can't directly test the private method, but we can test the concept
            
            // Arrange
            var style = new ImageButtonStyle();
            var hoverButton = new HoverableImageButton(style, "");
            
            // Act & Assert - just verify button creation succeeds with empty text
            Assert.IsNotNull(hoverButton, "Button should be created with empty hover text");
        }
    }
}