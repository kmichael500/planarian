import { Typography } from "antd";

const { Paragraph } = Typography;

export interface ParagraphDisplayProps {
  text: string | undefined | null;
  style?: React.CSSProperties;
}

const ParagraphDisplayComponent = ({ text, style }: ParagraphDisplayProps) => {
  const paragraphs = text?.split("\n");

  return (
    <Typography>
      {paragraphs?.map((paragraph, index) => (
        <Paragraph style={style} key={`paragraph ${index}`}>
          {paragraph}
        </Paragraph>
      ))}
    </Typography>
  );
};

export { ParagraphDisplayComponent };
