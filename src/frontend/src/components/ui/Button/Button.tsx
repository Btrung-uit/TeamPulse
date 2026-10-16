import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react'
import { LoaderCircle } from 'lucide-react'

import { classNames } from '@/lib/classNames'

export type ButtonVariant =
  'primary' | 'secondary' | 'ghost' | 'danger' | 'link'
export type ButtonSize = 'sm' | 'md' | 'lg'

interface ButtonBaseProps extends Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  'children'
> {
  variant?: ButtonVariant
  size?: ButtonSize
  isLoading?: boolean
  loadingLabel?: string
  leadingIcon?: ReactNode
  trailingIcon?: ReactNode
}

interface TextButtonProps extends ButtonBaseProps {
  iconOnly?: false
  children: ReactNode
}

interface IconButtonProps extends ButtonBaseProps {
  iconOnly: true
  children: ReactNode
  'aria-label': string
  title: string
}

export type ButtonProps = TextButtonProps | IconButtonProps

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  function Button(
    {
      variant = 'primary',
      size = 'md',
      isLoading = false,
      loadingLabel = 'Loading',
      leadingIcon,
      trailingIcon,
      iconOnly = false,
      className,
      disabled,
      children,
      type = 'button',
      ...props
    },
    ref,
  ) {
    return (
      <button
        {...props}
        ref={ref}
        type={type}
        className={classNames(
          'ui-button',
          `ui-button--${variant}`,
          `ui-button--${size}`,
          iconOnly && 'ui-button--icon-only',
          className,
        )}
        disabled={disabled || isLoading}
        aria-busy={isLoading || undefined}
        aria-label={isLoading ? loadingLabel : props['aria-label']}
      >
        {isLoading && (
          <span className="ui-button__loader" aria-hidden="true">
            <LoaderCircle size={18} />
          </span>
        )}
        <span
          className={classNames(
            'ui-button__content',
            isLoading && 'is-loading',
          )}
        >
          {leadingIcon && (
            <span className="ui-button__icon" aria-hidden="true">
              {leadingIcon}
            </span>
          )}
          {children}
          {trailingIcon && (
            <span className="ui-button__icon" aria-hidden="true">
              {trailingIcon}
            </span>
          )}
        </span>
        {isLoading && <span className="sr-only">{loadingLabel}</span>}
      </button>
    )
  },
)
