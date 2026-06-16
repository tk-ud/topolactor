import type { ComponentDesignParams } from "./types.ts";
import { mergeDesignClassName } from "./types.ts";

export type VideoPlayerProps = {
  src: string;
  mimeType?: string;
  title?: string;
  controls?: boolean;
  autoplay?: boolean;
  loop?: boolean;
  poster?: string;
  ariaLabel?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function VideoPlayer(props: VideoPlayerProps) {
  const controls = props.controls !== false;
  return (
    <figure class={mergeDesignClassName(props.className, props.design)}>
      {props.title && <figcaption class="mb-1 text-sm font-medium text-slate-700">{props.title}</figcaption>}
      <video
        src={props.src}
        controls={controls}
        autoplay={props.autoplay === true}
        loop={props.loop === true}
        poster={props.poster}
        aria-label={props.ariaLabel ?? props.title}
      >
        {props.mimeType && <source src={props.src} type={props.mimeType} />}
      </video>
    </figure>
  );
}
