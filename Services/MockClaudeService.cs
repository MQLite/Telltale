using Telltale.Models;

namespace Telltale.Services;

public class MockClaudeService(ILogger<MockClaudeService> logger) : IClaudeService
{
    public async Task<StoryResponse> GenerateStoryAsync(string keywords, string language)
    {
        logger.LogWarning("MockClaudeService is active — returning hardcoded story (keywords={Keywords})", keywords);

        await Task.Delay(800); // simulate network latency

        return new StoryResponse
        {
            TitleEn = "Hammy the Hammerhead Brushes His Teeth",
            TitleZh = "锤头鲨哈米刷牙啦",
            Pages =
            [
                new StoryPage
                {
                    PageNumber = 1,
                    SentencesEn =
                    [
                        new("Deep in the sparkling blue ocean lived a hammerhead shark named Hammy.", "warmly"),
                        new("Hammy had the widest smile in the whole sea, but his teeth were covered in leftover fish and seaweed!", "cheerfully"),
                        new("All the little fish swam far away from his stinky breath.", "sadly"),
                    ],
                    SentencesZh =
                    [
                        new("在闪闪发光的蓝色大海深处，住着一只叫哈米的锤头鲨。", "warmly"),
                        new("哈米有整片海洋里最宽的笑容，但他的牙齿上沾满了鱼骨和海草！", "cheerfully"),
                        new("所有小鱼都因为他的口臭而游得远远的。", "sadly"),
                    ],
                    ImagePrompt = "cartoon oil painting style, children's book illustration, warm soft colors, a friendly cartoonish hammerhead shark with a wide goofy grin showing dirty yellow teeth covered in tiny fish bones and green seaweed, colorful coral reef background, small fish holding their noses and swimming away, bubbles floating up, warm turquoise underwater lighting, cheerful and humorous mood"
                },
                new StoryPage
                {
                    PageNumber = 2,
                    SentencesEn =
                    [
                        new("One day, a kind little clownfish named Cleo swam up to Hammy.", "warmly"),
                        new("She gave him a giant coral toothbrush and showed him how to brush up, down, and in circles.", "excitedly"),
                        new("Hammy was nervous at first because he had never brushed his teeth before!", "mysteriously"),
                    ],
                    SentencesZh =
                    [
                        new("有一天，一条善良的小丑鱼克莱奥游到哈米面前。", "warmly"),
                        new("她递给他一把大大的珊瑚牙刷，教他怎么上下刷、转圈刷。", "excitedly"),
                        new("哈米一开始很紧张，因为他从来没有刷过牙！", "mysteriously"),
                    ],
                    ImagePrompt = "cartoon oil painting style, children's book illustration, warm soft colors, a tiny cheerful clownfish with orange and white stripes holding out a large pink coral toothbrush toward a wide-eyed nervous hammerhead shark, underwater ocean setting with soft golden light filtering from above, colorful sea anemones in the background, warm and encouraging mood, soft bubbles all around"
                },
                new StoryPage
                {
                    PageNumber = 3,
                    SentencesEn =
                    [
                        new("Hammy took the toothbrush and started scrubbing all of his big flat teeth.", "cheerfully"),
                        new("Minty ocean foam bubbles floated everywhere as he brushed and brushed!", "excitedly"),
                        new("He giggled because the toothbrush tickled his gums.", "cheerfully"),
                    ],
                    SentencesZh =
                    [
                        new("哈米接过牙刷，开始认真刷他所有又大又平的牙齿。", "cheerfully"),
                        new("他刷呀刷，薄荷味的泡泡飘得到处都是！", "excitedly"),
                        new("他忍不住咯咯笑，因为牙刷把他的牙龈搔得好痒。", "cheerfully"),
                    ],
                    ImagePrompt = "cartoon oil painting style, children's book illustration, warm soft colors, a happy hammerhead shark vigorously brushing his wide flat teeth with a big coral toothbrush, white minty foam bubbles floating all around him, his eyes squinted with laughter, small colorful fish watching in delight and cheering, bright underwater scene with rays of golden sunlight, playful and joyful mood"
                },
                new StoryPage
                {
                    PageNumber = 4,
                    SentencesEn =
                    [
                        new("When Hammy finished, his teeth sparkled like bright white pearls in the sunlight.", "triumphantly"),
                        new("All the little fish swam back to admire his dazzling smile and they all cheered!", "excitedly"),
                        new("From that day on, Hammy brushed his teeth every single morning and night.", "warmly"),
                    ],
                    SentencesZh =
                    [
                        new("哈米刷完牙后，他的牙齿像阳光下的白珍珠一样闪闪发亮。", "triumphantly"),
                        new("所有小鱼都游回来欣赏他那灿烂的笑容，大家一起欢呼！", "excitedly"),
                        new("从那天起，哈米每天早晚都认真刷牙。", "warmly"),
                    ],
                    ImagePrompt = "cartoon oil painting style, children's book illustration, warm soft colors, a proud and beaming hammerhead shark showing off his sparkling brilliant white teeth with a huge smile, surrounded by dozens of happy colorful tropical fish cheering and swimming joyfully, clownfish Cleo giving a thumbs up, bright sunny underwater scene with golden light beams, shimmering sparkle effects on the teeth, warm celebratory and heartwarming mood"
                }
            ]
        };
    }
}
