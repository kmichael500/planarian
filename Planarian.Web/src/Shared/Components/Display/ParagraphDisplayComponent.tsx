import { Typography } from "antd";

const { Paragraph } = Typography;

export interface ParagraphDisplayProps {
  text: string | undefined | null;
}

const ParagraphDisplayComponent = ({ text }: ParagraphDisplayProps) => {
  const paragraphs = text?.split("\n");

  return (
    <Typography>
      {paragraphs?.map((paragraph, index) => (
        <Paragraph key={`paragraph ${index}`}>{paragraph}</Paragraph>
      ))}
    </Typography>
  );
};

export { ParagraphDisplayComponent };
